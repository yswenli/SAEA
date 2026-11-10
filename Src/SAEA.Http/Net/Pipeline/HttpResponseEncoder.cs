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
 *命名空间：SAEA.Http
 *文件名： HttpResponseEncoder
 *版本号： v26.11.5.1
 *唯一标识：2986763d-6aae-43d0-9920-35451cedff7a
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 13:19:30
 *描述：HTTP 出站编码器：把 HttpResponse 编码成原始帧交给链头
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/9 13:19:30
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：HTTP 出站编码器：把 HttpResponse 编码成原始帧交给链头
 *
 *****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 出站编码器：把 <see cref="HttpResponse"/> 编码成 <see cref="RawFrame"/>。
    /// <para>
    /// 出站前缀节点，必须装在 <see cref="OutboundSerializingHandler"/> 左侧（更靠近链首），
    /// 这样串行器先排队、本节点再编码，异步编码不会打乱同一连接上的响应顺序。
    /// </para>
    /// <para>
    /// 只生成字节并交给链头，不写 socket：真正的发送由链头出站终点完成，
    /// 背压、串行化与所有权移交都留在链上。
    /// </para>
    /// </summary>
    public sealed class HttpResponseEncoder : OutboundHandlerAdapter
    {
        /// <summary>
        /// 编码一次出站写：响应对象编码成原始帧，已经是原始帧的消息（升级后的原始字节）原样透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="HttpResponse"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var response = msg as HttpResponse;
            if (response != null)
            {
                // 编码结果自带独立数组，不持有池化缓冲，因此没有需要随帧归还的所有者。
                return ctx.WriteAsync(new RawFrame(response.Encode(), null));
            }

            if (msg is RawFrame)
                return ctx.WriteAsync(msg);

            throw new InvalidOperationException(
                "HttpResponseEncoder 只接受 HttpResponse 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }
    }
}