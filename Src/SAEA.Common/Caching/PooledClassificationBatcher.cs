/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Common.Caching
*文件名： PooledClassificationBatcher
*版本号： v26.11.5.1
*唯一标识：c537ac92-4169-47e2-abc8-48bd932e5b79
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：基于内存池的分类批量处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 按 id 分类的池化批处理器。每个 id 一个 <see cref="PooledBatcher"/>，回调携带 id。
    /// </summary>
    /// <param name="id">分类标识</param>
    /// <param name="batch">合并后的 writer，回调返回后由内部释放，需在回调内取走数据</param>
    /// <param name="count">本批包含的 writer 个数</param>
    public delegate void OnPooledClassificationBatchedHandler(string id, PooledBufferWriter batch, int count);

    /// <summary>
    /// 池化分类批处理器，替代全局单例 <see cref="ClassificationBatcher"/> 以解除跨子系统参数耦合。
    /// </summary>
    public sealed class PooledClassificationBatcher : IDisposable
    {
        readonly ConcurrentDictionary<string, Lazy<PooledBatcher>> _dic;
        readonly int _size;
        readonly int _timeout;
        readonly int _max;
        volatile bool _disposed;

        /// <summary>
        /// 某个分类的数据成批后触发，参数为分类名、合并后的 writer 与本批条数；未订阅时合并结果直接被释放
        /// </summary>
        public event OnPooledClassificationBatchedHandler OnBatched;

        /// <summary>
        /// 初始化分类批处理器，各分类的批处理实例在首次入队时按相同参数创建
        /// </summary>
        /// <param name="size">每个分类每批最大 writer 个数，小于等于 0 时按 1000 处理</param>
        /// <param name="timeout">凑不满一批时的最长等待时间（毫秒），小于等于 0 时按 1000 处理</param>
        /// <param name="max">每个分类队列允许积压的最大 writer 个数，-1 表示按 size × 10 计算</param>
        public PooledClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1)
        {
            _size = size > 0 ? size : 1000;
            _timeout = timeout > 0 ? timeout : 1000;
            _max = max == -1 ? _size * 10 : max;
            if (_max < _size) throw new ArgumentOutOfRangeException(nameof(max), "max不能小于size");
            _dic = new ConcurrentDictionary<string, Lazy<PooledBatcher>>();
        }

        PooledBatcher GetOrCreate(string id)
        {
            var lazy = _dic.GetOrAdd(id, n => new Lazy<PooledBatcher>(() =>
            {
                var b = new PooledBatcher(_size, _timeout, _max, n);
                var captured = n;
                b.OnBatched += (w, count) =>
                {
                    var h = OnBatched;
                    if (h != null) h(captured, w, count);
                    else w.Dispose();
                };
                return b;
            }));
            return lazy.Value;
        }

        /// <summary>
        /// 向指定分类入队一个 writer，id 为空、writer 为 null、已释放或队列满时返回 false，此时 writer 仍归调用方所有
        /// </summary>
        /// <param name="id">分类标识</param>
        /// <param name="writer">待入队的 writer</param>
        /// <returns>入队成功返回 true，此后 writer 由本类释放</returns>
        public bool Insert(string id, PooledBufferWriter writer)
        {
            if (string.IsNullOrEmpty(id) || writer == null) return false;
            if (_disposed) return false;
            return GetOrCreate(id).Insert(writer);
        }

        /// <summary>
        /// 停止并移除指定分类的批处理实例，队列内未处理的数据被丢弃
        /// </summary>
        /// <param name="id">分类标识，为空时直接返回</param>
        public void Clear(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_dic.TryRemove(id, out var lazy) && lazy.IsValueCreated) lazy.Value.Dispose();
        }

        /// <summary>
        /// 停止并释放全部分类的批处理实例，队列内未处理的数据被丢弃
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            foreach (var kv in _dic)
            {
                try { if (kv.Value.IsValueCreated) kv.Value.Value.Dispose(); } catch { }
            }
            _dic.Clear();
        }
    }
}
