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
*文件名： ByteToMessageDecoder
*版本号： v26.11.5.1
*唯一标识：ed34c9d5-7e9b-4af3-a9b5-2e79d283b652
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:04:09
*描述：把一帧解成 0..N 条业务消息的入站解码器基类
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 15:28:50
*修改人： yswenli
*版本号： v26.11.16.1
*描述：单条输出走同步派发快路径，输出容器按解码器实例复用
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 把一帧解成 0..N 条业务消息的入站解码器基类。
    /// <para>
    /// 输入必须是由泵切好的完整帧 <see cref="PooledMessage"/>，基类<b>不负责累加半包</b>：
    /// 半包由接收泵拦在 <c>Pipe</c> 里，留给下一次读取。
    /// </para>
    /// <para>
    /// 所有权：基类<b>不释放输入帧</b>。队列条目那一份原始引用始终由执行器
    /// （或纯链式场景的生产者）释放。默认输出应自带内存；若要把原始帧交给下游，
    /// 必须每放一个进 <paramref name="output"/> 就调用一次 <see cref="PooledMessage.Retain"/>，
    /// 多条输出共享同一帧就 Retain 多次，由最终消费方各释放一份。
    /// 每个输出开始派发时即按「链持有」记账：即使与输入帧是同一实例，下游失败或取消也由入站边界补偿该项。
    /// 下游若要跨回调持有消息或帧字节，必须自行再 <see cref="PooledMessage.Retain"/>。
    /// </para>
    /// <para>
    /// 派发顺序：<see cref="Decode"/> 返回后基类按 <c>output</c> 下标逐条
    /// <c>await ctx.FireChannelReadAsync</c>；某条一开始派发即所有权移交，之后该条由入站边界或消费方负责。
    /// 某条派发失败或取消时基类停止继续派发，并释放尚未开始的余项，异常只向上冒泡一次。
    /// </para>
    /// <para>
    /// 解码成功且恰好一条输出时走同步快路径：下游返回的任务原样交回调用方，不再额外等待一次；
    /// 这一支没有余项，因此不需要释放动作。零条或多条输出、解码失败仍走逐条派发的慢路径。
    /// </para>
    /// </summary>
    public abstract class ByteToMessageDecoder : InboundHandlerAdapter
    {
        // 每帧通常只产出一条消息，列表仅作容器；复用一份可省下每次分配。
        // 但同一连接可能有多帧同时在途，用占用标记保证同一时刻只有一帧在用这份列表。
        private List<object> _reusableOutput;
        private int _outputClaimed;

        /// <summary>
        /// 把一帧解成 0..N 条消息追加到 <paramref name="output"/>；零条表示本帧不产生业务消息。
        /// 实现必须是同步的，并且不得把帧内存直接暴露给下游，除非按基类说明逐条 Retain。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">由泵切好的完整帧，基类不释放。</param>
        /// <param name="output">本次解码要按序派发的消息列表；列表由基类持有并复用，实现不得保存该引用。</param>
        protected abstract void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output);

        /// <summary>
        /// 只接受 <see cref="PooledMessage"/>；先解码，再按序派发，最后把解码异常冒泡给调用方。
        /// 解码成功且恰好一条输出时同步把下游任务交回调用方，不再套一层状态机。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">完整帧消息，必须是 <see cref="PooledMessage"/>。</param>
        /// <returns>全部输出处理完成后结束的任务；单条输出时就是下游返回的那个任务。</returns>
        public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var frame = msg as PooledMessage;
            if (frame == null)
                throw new InvalidOperationException("ByteToMessageDecoder 的入参必须是 PooledMessage。");

            bool reusable;
            var output = RentOutput(out reusable);
            Exception decodeFailure = null;

            try
            {
                Decode(ctx, frame, output);
            }
            catch (Exception ex)
            {
                // 取消与普通异常都先记下、最后原样冒泡；普通异常的下游通知是异步的，统一交给慢路径。
                decodeFailure = ex;
            }

            if (decodeFailure == null && output.Count == 1)
            {
                var message = output[0];

                // 下游拿到的只是这一条消息的引用，派发开始后不会再回头读容器；
                // 所以派发之前就能清空并归还，复用不会把本帧消息带进下一次派发。
                ReturnOutput(output, reusable);
                return DispatchSingle(ctx, message);
            }

            return DispatchAsync(ctx, output, reusable, decodeFailure);
        }

        /// <summary>
        /// 慢路径：零条或多条输出，以及解码失败的情形。
        /// 逐条按序派发、失败时释放未开始的余项、最后重抛解码异常，与单条快路径之外的行为完全一致。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="output">本次解码的输出列表。</param>
        /// <param name="reusable">输出列表是否来自实例复用槽，决定结束时归还还是丢弃。</param>
        /// <param name="decodeFailure">解码阶段捕获的异常；无异常时为 null。</param>
        /// <returns>全部输出处理完成后结束的任务。</returns>
        private async Task DispatchAsync(IChannelHandlerContext ctx, List<object> output, bool reusable, Exception decodeFailure)
        {
            try
            {
                if (decodeFailure != null && !(decodeFailure is OperationCanceledException))
                {
                    // 通知自身失败只记录，不能替换或吞掉原始解码异常。
                    try
                    {
                        await ctx.FireExceptionCaughtAsync(decodeFailure).ConfigureAwait(false);
                    }
                    catch (Exception notifyError)
                    {
                        LogHelper.Debug("解码异常通知失败:" + (ctx == null ? string.Empty : ctx.ChannelId) + " " + notifyError.Message);
                    }
                }

                var chainOwnedContext = ctx as IInboundChainOwnedContext;

                for (var i = 0; i < output.Count; i++)
                {
                    try
                    {
                        // 解码器输出每一项都持有一份引用，即使与输入帧是同一实例也必须按链持有派发，
                        // 否则下游失败时入站边界会误判为执行器持有而不补偿。
                        if (chainOwnedContext != null)
                            await chainOwnedContext.FireChainOwnedChannelReadAsync(output[i]).ConfigureAwait(false);
                        else
                            await ctx.FireChannelReadAsync(output[i]).ConfigureAwait(false);
                    }
                    catch (Exception dispatchFailure)
                    {
                        // 当前项已开始派发，所有权归入站链；只释放尚未开始的余项。
                        ReleaseRemaining(output, i + 1);
                        ExceptionDispatchInfo.Capture(dispatchFailure).Throw();
                    }
                }

                if (decodeFailure != null)
                    ExceptionDispatchInfo.Capture(decodeFailure).Throw();
            }
            finally
            {
                // 列表在跨 await 期间一直被占用：余项释放还要按原下标读取内容，只能在这里归还。
                ReturnOutput(output, reusable);
            }
        }

        /// <summary>把唯一的解码结果交给下游，并把下游返回的任务原样交回调用方。</summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="message">本次解码产出的唯一一条消息。</param>
        /// <returns>下游处理完成后结束的任务，与下游返回的是同一个实例。</returns>
        private static Task DispatchSingle(IChannelHandlerContext ctx, object message)
        {
            // 解码器输出每一项都持有一份引用，即使与输入帧是同一实例也必须按链持有派发，
            // 否则下游失败时入站边界会误判为执行器持有而不补偿。
            var chainOwnedContext = ctx as IInboundChainOwnedContext;
            if (chainOwnedContext != null)
                return chainOwnedContext.FireChainOwnedChannelReadAsync(message);

            return ctx.FireChannelReadAsync(message);
        }

        /// <summary>
        /// 取本次解码的输出容器：抢到复用标记就用实例上那一份，否则新建一份只供本次使用的列表。
        /// 同一连接可能有多帧同时在途，抢不到标记说明上一帧还没派发完，不能共用同一个列表。
        /// </summary>
        /// <param name="reusable">输出容器是否来自复用槽。</param>
        /// <returns>已清空的输出容器。</returns>
        private List<object> RentOutput(out bool reusable)
        {
            if (Interlocked.CompareExchange(ref _outputClaimed, 1, 0) == 0)
            {
                var output = _reusableOutput;
                if (output == null)
                {
                    output = new List<object>();
                    _reusableOutput = output;
                }
                else
                {
                    output.Clear();
                }

                reusable = true;
                return output;
            }

            reusable = false;
            return new List<object>();
        }

        /// <summary>清空并归还复用容器；没抢到标记的私有列表直接丢弃，交给 GC 回收。</summary>
        /// <param name="output">本次解码的输出容器。</param>
        /// <param name="reusable">该容器是否来自复用槽。</param>
        private void ReturnOutput(List<object> output, bool reusable)
        {
            if (!reusable)
                return;

            // 先清内容再放开标记：下一个复用者拿到的容器不能还钉着上一帧的消息。
            output.Clear();
            Volatile.Write(ref _outputClaimed, 0);
        }

        /// <summary>从指定下标起释放尚未开始派发的余项，单项释放异常只记录，不阻断后续释放。</summary>
        /// <param name="output">本次解码的输出列表。</param>
        /// <param name="start">第一个尚未开始派发的下标。</param>
        private static void ReleaseRemaining(List<object> output, int start)
        {
            for (var i = start; i < output.Count; i++)
            {
                var disposable = output[i] as IDisposable;
                if (disposable == null)
                    continue;

                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("解码器余项释放失败:" + output[i].GetType().FullName, ex);
                }
            }
        }
    }
}