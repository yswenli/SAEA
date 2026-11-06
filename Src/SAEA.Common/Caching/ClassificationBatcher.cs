/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common.Caching
*文件名： ClassificationBatcher
*版本号： v26.4.23.1
*唯一标识：503f9913-be90-408e-9129-e169eb000d12
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/23 17:18:02
*描述：ClassificationBatcher接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Threading;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 按分类标识回调的字节数组批次
    /// </summary>
    /// <param name="id">分类标识</param>
    /// <param name="data">该分类本批次按入队顺序拼接后的连续字节</param>
    public delegate void OnClassificationBatchedHandler(string id, byte[] data);

    /// <summary>
    /// 按分类维护多个字节数组批处理队列，同一分类的数据合并成批后回调
    /// </summary>
    public sealed class ClassificationBatcher : IDisposable
    {
        ConcurrentDictionary<string, Batcher> _dic;
        ConcurrentDictionary<string, SemaphoreSlim> _semaphores;

        int _size, _timeout, _max, _concurrencyLimit;

        /// <summary>
        /// 某个分类的数据成批后触发，参数为分类名与本批合并后的字节
        /// </summary>
        public event OnClassificationBatchedHandler OnBatched;

        /// <summary>
        /// 初始化分类批处理器，各分类的批处理实例在首次入队时按相同参数创建
        /// </summary>
        /// <param name="size">每个分类每批最大数据段数</param>
        /// <param name="timeout">凑不满一批时的最长等待时间（毫秒）</param>
        /// <param name="max">每个分类队列允许积压的最大段数，-1 表示按 size × 10 计算</param>
        /// <param name="concurrencyLimit">每个分类的回调并发上限</param>
        public ClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1, int concurrencyLimit = 10)
        {
            _size = size;
            _timeout = timeout;
            _max = max;
            if (_max == -1) _max = _size * 10;
            if (_max < _size) throw new ArgumentOutOfRangeException("max不能小于size");
            _concurrencyLimit = concurrencyLimit;
            _dic = new ConcurrentDictionary<string, Batcher>();
            _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        }


        #region static

        volatile static ClassificationBatcher _classificationBatcher = null;

        static object _locker = new object();

        /// <summary>
        /// 获取进程内单例实例，首次调用的参数决定实例配置，后续调用的参数被忽略
        /// </summary>
        /// <param name="size">每个分类每批最大数据段数</param>
        /// <param name="timeout">凑不满一批时的最长等待时间（毫秒）</param>
        /// <param name="max">每个分类队列允许积压的最大段数，-1 表示按 size × 10 计算</param>
        /// <param name="concurrencyLimit">每个分类的回调并发上限</param>
        /// <returns>单例实例</returns>
        public static ClassificationBatcher GetInstance(int size = 1000, int timeout = 1000, int max = -1, int concurrencyLimit = 10)
        {
            if (_classificationBatcher == null)
            {
                lock (_locker)
                {
                    if (_classificationBatcher == null)
                    {
                        _classificationBatcher = new ClassificationBatcher(size, timeout, max, concurrencyLimit);
                    }
                }
            }
            return _classificationBatcher;
        }

        #endregion

        private SemaphoreSlim GetOrCreateSemaphore(string name)
        {
            return _semaphores.GetOrAdd(name, _ => new SemaphoreSlim(_concurrencyLimit, _concurrencyLimit));
        }

        private Batcher GetOrCreateBatcher(string name)
        {
            return _dic.GetOrAdd(name, n =>
            {
                var b = new Batcher(_size, _timeout, _max, n, _concurrencyLimit);
                b.OnBatched += Bacher_OnBatched;
                return b;
            });
        }

        /// <summary>
        /// 向指定分类入队一段字节，该分类队列满或已停止时立即返回 false
        /// </summary>
        /// <param name="name">分类名，不存在时自动创建对应队列</param>
        /// <param name="data">待入队的数据段</param>
        /// <returns>是否入队成功</returns>
        public bool Insert(string name, byte[] data)
        {
            var bacher = GetOrCreateBatcher(name);
            return bacher.Insert(data);
        }

        /// <summary>
        /// 向指定分类入队一段字节，语义与同步入队一致，取消令牌当前不生效
        /// </summary>
        /// <param name="name">分类名，不存在时自动创建对应队列</param>
        /// <param name="data">待入队的数据段</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否入队成功</returns>
        public async Task<bool> InsertAsync(string name, byte[] data, CancellationToken cancellationToken = default)
        {
            var bacher = GetOrCreateBatcher(name);
            return await bacher.InsertAsync(data, cancellationToken);
        }

        /// <summary>
        /// 向指定分类入队一段字节，队列满时在超时内等待容量名额
        /// </summary>
        /// <param name="name">分类名，不存在时自动创建对应队列</param>
        /// <param name="data">待入队的数据段</param>
        /// <param name="timeoutMs">等待容量名额的超时时间（毫秒）</param>
        /// <param name="cancellationToken">外部取消令牌</param>
        /// <returns>入队成功返回 true，超时、取消或分类已停止返回 false</returns>
        public async Task<bool> InsertWithBackpressureAsync(string name, byte[] data, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            var bacher = GetOrCreateBatcher(name);
            return await bacher.InsertWithBackpressureAsync(data, timeoutMs, cancellationToken);
        }

        /// <summary>
        /// 判断指定分类的队列是否已满，分类不存在时返回 false
        /// </summary>
        /// <param name="name">分类名</param>
        /// <returns>队列已满返回 true</returns>
        public bool IsFull(string name)
        {
            if (_dic.TryGetValue(name, out Batcher b))
            {
                return b.IsFull;
            }
            return false;
        }

        /// <summary>
        /// 获取指定分类队列中尚未取出的数据段个数，分类不存在时返回 0
        /// </summary>
        /// <param name="name">分类名</param>
        /// <returns>待处理段数</returns>
        public int GetPendingCount(string name)
        {
            if (_dic.TryGetValue(name, out Batcher b))
            {
                return b.PendingCount;
            }
            return 0;
        }

        private void Bacher_OnBatched(IBatcher sender, byte[] data)
        {
            var bacher = (Batcher)sender;
            var semaphore = GetOrCreateSemaphore(bacher.Name);

            TaskHelper.Run(async () =>
            {
                try
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        OnBatched?.Invoke(bacher.Name, data);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        /// <summary>
        /// 停止并移除指定分类的批处理队列，同时释放该分类的回调信号量，队列内未处理的数据被丢弃
        /// </summary>
        /// <param name="name">分类名</param>
        public void Clear(string name)
        {
            if (_dic.TryGetValue(name, out Batcher b))
            {
                if (b != null)
                {
                    b.OnBatched -= Bacher_OnBatched;
                    b.Dispose();
                    _dic.TryRemove(name, out _);
                }
            }
            if (_semaphores.TryGetValue(name, out SemaphoreSlim sem))
            {
                sem.Dispose();
                _semaphores.TryRemove(name, out _);
            }
        }

        /// <summary>
        /// 停止并释放全部分类的批处理队列与信号量，队列内未处理的数据被丢弃
        /// </summary>
        public void Dispose()
        {
            foreach (var item in _dic)
            {
                item.Value.OnBatched -= Bacher_OnBatched;
                item.Value.Dispose();
            }
            _dic.Clear();

            foreach (var item in _semaphores)
            {
                item.Value.Dispose();
            }
            _semaphores.Clear();
        }
    }
}