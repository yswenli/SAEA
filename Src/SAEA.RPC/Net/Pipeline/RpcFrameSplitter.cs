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
*命名空间：SAEA.RPC
*文件名： RpcFrameSplitter
*版本号： v26.11.5.1
*唯一标识：edc7a8f0-cc79-4f26-bf29-db092311bff8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 15:00:00
*描述：RPC 线格式的拆帧配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，RPC 线格式 <c>[1B type][4B total]...</c> 的拆帧配置
*
*****************************************************************************/
using System;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC
{
    /// <summary>
    /// RPC 线格式 <c>[1B type@0][4B total@1][8B sequence][4B serviceLen][service][4B methodLen][method][data]</c> 的拆帧配置。
    /// <para>
    /// <c>total</c> 含长度字段自身及其后全部字段，整帧长度为 <c>1 + total</c>，因此修正值取 <c>-4</c>；
    /// 产出帧保留完整头部（<c>initialBytesToStrip = 0</c>），解码器直接按帧读全部字段。
    /// </para>
    /// </summary>
    public static class RpcFrameSplitter
    {
        /// <summary>
        /// RPC 帧固定头部字节数：1B 类型 + 4B 总长度。
        /// </summary>
        public const int HeaderLength = 5;

        /// <summary>
        /// 创建 RPC 分帧器：<c>1 / 4 / -4 / 0</c>，小端。
        /// </summary>
        /// <param name="maxFrameLength">长度字段口径的帧长上限（字节），非负</param>
        /// <returns>无状态的拆帧器实例，可按连接复用</returns>
        public static LengthFieldSplitter Create(int maxFrameLength)
        {
            return new LengthFieldSplitter(
                maxFrameLength,
                1,
                4,
                -4,
                0,
                true);
        }

        /// <summary>
        /// 取长度字段口径的帧长上限：沿用 <see cref="BaseCoder.MaxFrameLength"/> 的可配置口径，
        /// 并按与既有 <c>FrameDecoder</c> 相同的规则夹逼，避免整帧长度在 int 运算中溢出。
        /// </summary>
        /// <returns>本次连接使用的帧长上限（字节）</returns>
        public static int ResolveMaxFrameLength()
        {
            var configured = BaseCoder.MaxFrameLength;

            return configured < FrameConstants.P_Head
                ? int.MaxValue - FrameConstants.P_Head
                : Math.Min(configured, int.MaxValue - FrameConstants.P_Head);
        }
    }
}