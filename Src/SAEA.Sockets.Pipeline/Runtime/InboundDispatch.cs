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
*文件名： InboundDispatch
*版本号： v26.11.5.1
*唯一标识：196b50c6-3341-4c51-aa09-971701a42392
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:04:09
*描述：单次入站派发的所有权与异常通知状态，按调用边界记录转发结果
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:04:09
*修改人： yswenli
*版本号： v26.11.5.1
*描述：单次入站派发的所有权与异常通知状态，按调用边界记录转发结果
*
*****************************************************************************/
using System.Threading;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 单次入站派发的所有权与异常通知状态。
    /// <para>
    /// 每次从上游节点交给下一节点都创建一个实例，<see cref="Forwarded"/> 只描述该边界是否已经把
    /// 消息交给下游；异常通知标记则由同一次消息派发的所有边界共享，保证上游看到下游已通知后不再重复通知。
    /// </para>
    /// <para>
    /// 所有权分三种来源：执行器派发的原始帧（<see cref="ChainOwned"/> 为 <c>false</c>，始终由执行器
    /// <c>finally</c> 释放）、解码器等链上节点产出的新消息（<see cref="ChainOwned"/> 为 <c>true</c>）
    /// 以及处理器替换出来的新对象（同样按链持有处理）。只有链持有的消息会在未成功转发时被边界补偿。
    /// </para>
    /// </summary>
    internal sealed class InboundDispatch
    {
        /// <summary>同一次消息派发共享的异常通知标记；用引用传递，避免逐边界复制后失真。</summary>
        private sealed class NotifyState
        {
            private int _notified;

            public bool Notified
            {
                get { return Volatile.Read(ref _notified) != 0; }
            }

            public void Mark()
            {
                Volatile.Write(ref _notified, 1);
            }
        }

        private readonly NotifyState _notify;

        private InboundDispatch(NotifyState notify, bool chainOwned, object operation)
        {
            _notify = notify;
            ChainOwned = chainOwned;
            Operation = operation;
        }

        /// <summary>消息是否由链持有；为 <c>false</c> 表示执行器持有的原始帧。</summary>
        public bool ChainOwned { get; private set; }

        /// <summary>本调用边界是否已把消息交给下一节点；交给下游后补偿由下游边界负责。</summary>
        public bool Forwarded { get; set; }

        /// <summary>同一次消息派发是否已经向下游通知过异常。</summary>
        public bool ExceptionNotified
        {
            get { return _notify.Notified; }
        }

        /// <summary>
        /// 本次派发关联的操作对象。业务写入口与出站操作对象尚未接入，此处保留 <c>null</c>，
        /// 待出站操作对象落地后与入站派发绑定同一个操作。
        /// </summary>
        public object Operation { get; private set; }

        /// <summary>创建执行器持有的原始帧派发；该消息的补偿不由入站链承担。</summary>
        /// <param name="operation">关联的操作对象，暂为 null。</param>
        /// <returns>新派发状态。</returns>
        internal static InboundDispatch CreateExecutorOwned(object operation)
        {
            return new InboundDispatch(new NotifyState(), false, operation);
        }

        /// <summary>创建链持有的消息派发；未成功转发时由该边界释放。</summary>
        /// <param name="operation">关联的操作对象，暂为 null。</param>
        /// <returns>新派发状态。</returns>
        internal static InboundDispatch CreateChainOwned(object operation)
        {
            return new InboundDispatch(new NotifyState(), true, operation);
        }

        /// <summary>
        /// 派生下游边界的派发状态：与上游共享异常通知标记，所有权按本次消息重新判定。
        /// </summary>
        /// <param name="chainOwned">下游消息是否由链持有。</param>
        /// <returns>下游边界的派发状态。</returns>
        internal InboundDispatch Derive(bool chainOwned)
        {
            return new InboundDispatch(_notify, chainOwned, Operation);
        }

        /// <summary>标记本次消息派发已经向下游通知过异常，上游不再重复通知。</summary>
        internal void MarkExceptionNotified()
        {
            _notify.Mark();
        }
    }
}