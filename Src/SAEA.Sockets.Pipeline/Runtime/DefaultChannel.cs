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
*命名空间：SAEA.Sockets.Pipeline
*文件名： DefaultChannel
*版本号： v26.11.5.1
*唯一标识：8659570d-a875-4c7b-accb-c19adba22946
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：IChannel 的默认实现：每连接一个实例，属性用并发字典背衬
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:21:25
*修改人： yswenli
*版本号： v26.11.24.2
*描述：排空字节数改为长整型，跨线程读写改用 Interlocked 交换
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// <see cref="IChannel"/> 的默认实现：每连接一个实例，属性用并发字典背衬。
    /// <para>
    /// <see cref="CloseAsync"/> 只表示"已请求关闭"：第一次调用会把 <see cref="IsActive"/> 置为 false
    /// 并转调注入的关闭委托，重复调用不再触发委托。
    /// 清空属性属于收口流程的末端：失活只翻转活跃状态，属性要等 <see cref="ClearAttributes"/> 才释放。
    /// </para>
    /// </summary>
    public sealed class DefaultChannel : IChannel, IDrainableChannel, IDisposable
    {
        private readonly ConcurrentDictionary<string, object> _attributes =
            new ConcurrentDictionary<string, object>();

        private readonly Func<string, Task> _closeFunc;

        private volatile bool _isActive = true;

        private int _closeRequested;

        private int _attributesCleared;

        // 排空规格由协议处理器在写错误响应后设置、收口末端取走，读写分处不同线程，统一走 Interlocked/Volatile。
        private long _drainBytes;

        private int _drainTimeoutMs;

        private int _drainSpecSet;

        /// <summary>
        /// 创建连接视图。
        /// </summary>
        /// <param name="id">连接标识。</param>
        /// <param name="closeFunc">关闭委托，接收关闭原因；为 null 时只置为非活跃，便于单测。</param>
        public DefaultChannel(string id, Func<string, Task> closeFunc)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            Id = id;
            _closeFunc = closeFunc;
        }

        /// <summary>连接标识。</summary>
        public string Id { get; }

        /// <summary>连接是否可继续收发；关闭请求生效或执行器标记收口后为 false。</summary>
        public bool IsActive
        {
            get { return _isActive; }
        }

        /// <summary>
        /// 连接级属性的实时只读视图，由并发字典背衬，可与其他线程的写入并发读取。
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes
        {
            get { return _attributes; }
        }

        /// <summary>写入连接级属性；同名键覆盖旧值。</summary>
        /// <param name="key">属性键，不能为 null。</param>
        /// <param name="value">属性值，允许为 null。</param>
        public void SetAttribute(string key, object value)
        {
            _attributes[key] = value;
        }

        /// <summary>读取连接级属性；未设置返回 null。</summary>
        /// <param name="key">属性键。</param>
        /// <returns>属性值；未设置时为 null。</returns>
        public object GetAttribute(string key)
        {
            object value;
            return _attributes.TryGetValue(key, out value) ? value : null;
        }

        /// <summary>移除连接级属性；键不存在返回 false。</summary>
        /// <param name="key">属性键。</param>
        /// <param name="value">被移除的属性值；键不存在时为 null。</param>
        /// <returns>是否成功移除。</returns>
        public bool TryRemoveAttribute(string key, out object value)
        {
            return _attributes.TryRemove(key, out value);
        }

        /// <summary>
        /// 请求关闭连接，幂等：只有第一次调用会置 <see cref="IsActive"/> 为 false 并转调关闭委托，
        /// 之后直接返回已完成任务。返回只表示已进入收口流程，不表示资源回收完成。
        /// </summary>
        /// <param name="reason">关闭原因，原样传给关闭委托。</param>
        /// <returns>关闭委托返回的任务；重复调用时为已完成任务。</returns>
        public Task CloseAsync(string reason)
        {
            if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
                return Task.CompletedTask;

            _isActive = false;

            return _closeFunc != null ? _closeFunc(reason) : Task.CompletedTask;
        }

        /// <summary>
        /// 执行器进入收口时调用：只把连接置为非活跃，属性保留给 <see cref="ChannelInactiveAsync"/> 读取。
        /// 可重复调用，不触发关闭委托。
        /// </summary>
        internal void MarkInactive()
        {
            _isActive = false;
        }

        /// <summary>清空连接级属性，仅供收口流程在失活通知与登记摘除完成后调用；只生效一次。</summary>
        internal void ClearAttributes()
        {
            if (Interlocked.Exchange(ref _attributesCleared, 1) != 0)
                return;

            _attributes.Clear();
        }

        /// <summary>
        /// 设置排空规格；任一参数非正时忽略。后一次设置覆盖前一次，收口时只会取走最近一次。
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数。</param>
        /// <param name="timeoutMs">排空超时毫秒数。</param>
        public void SetDrainSpec(long drainBytes, int timeoutMs)
        {
            if (drainBytes <= 0 || timeoutMs <= 0)
                return;

            Volatile.Write(ref _drainTimeoutMs, timeoutMs);
            Interlocked.Exchange(ref _drainBytes, drainBytes);
            Volatile.Write(ref _drainSpecSet, 1);
        }

        /// <summary>
        /// 取走排空规格并清空；只在第一次调用时返回 true，保证同一次收口不会重复排空。
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数；无规格时为 0。</param>
        /// <param name="timeoutMs">排空超时毫秒数；无规格时为 0。</param>
        /// <returns>存在有效规格时返回 true。</returns>
        public bool TryTakeDrainSpec(out long drainBytes, out int timeoutMs)
        {
            drainBytes = 0;
            timeoutMs = 0;

            if (Interlocked.Exchange(ref _drainSpecSet, 0) == 0)
                return false;

            drainBytes = Interlocked.Exchange(ref _drainBytes, 0);
            timeoutMs = Volatile.Read(ref _drainTimeoutMs);

            Volatile.Write(ref _drainTimeoutMs, 0);

            return drainBytes > 0 && timeoutMs > 0;
        }

        /// <summary>属性清理的生效次数，仅供测试与诊断读取。</summary>
        internal int AttributesClearCount
        {
            get { return Volatile.Read(ref _attributesCleared); }
        }

        /// <summary>清空连接级属性并丢弃引用；不触发关闭委托。</summary>
        public void Dispose()
        {
            _attributes.Clear();
        }
    }
}