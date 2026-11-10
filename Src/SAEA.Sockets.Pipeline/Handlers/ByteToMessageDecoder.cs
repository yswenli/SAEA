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
*修改时间：2026/11/9 11:12:58
*修改人： yswenli
*版本号： v26.11.5.1
*描述：把一帧解成 0..N 条业务消息的入站解码器基类，输出按链持有派发
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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
    /// </summary>
    public abstract class ByteToMessageDecoder : InboundHandlerAdapter
    {
        /// <summary>
        /// 把一帧解成 0..N 条消息追加到 <paramref name="output"/>；零条表示本帧不产生业务消息。
        /// 实现必须是同步的，并且不得把帧内存直接暴露给下游，除非按基类说明逐条 Retain。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">由泵切好的完整帧，基类不释放。</param>
        /// <param name="output">本次解码要按序派发的消息列表。</param>
        protected abstract void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output);

        /// <summary>
        /// 只接受 <see cref="PooledMessage"/>；先解码，再逐条按序派发，最后把解码异常冒泡给调用方。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">完整帧消息，必须是 <see cref="PooledMessage"/>。</param>
        /// <returns>全部输出处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var frame = msg as PooledMessage;
            if (frame == null)
                throw new InvalidOperationException("ByteToMessageDecoder 的入参必须是 PooledMessage。");

            var output = new List<object>();
            Exception decodeFailure = null;

            try
            {
                Decode(ctx, frame, output);
            }
            catch (OperationCanceledException ex)
            {
                // 取消不做异常通知，但仍派发已追加的消息并原样冒泡。
                decodeFailure = ex;
            }
            catch (Exception ex)
            {
                decodeFailure = ex;

                // 通知自身失败只记录，不能替换或吞掉原始解码异常。
                try
                {
                    await ctx.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
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