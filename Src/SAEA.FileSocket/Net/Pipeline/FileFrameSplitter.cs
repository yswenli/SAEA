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
*命名空间：SAEA.FileSocket
*文件名： FileFrameSplitter
*版本号： v26.11.5.1
*唯一标识：d283e055-081b-4bfb-b848-e1def8bac197
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 11:20:00
*描述：文件传输线格式的拆帧配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 11:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，集中文件传输线格式的拆帧口径
*
*****************************************************************************/
using System;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 文件传输线格式 <c>[8B bodyLength@0][1B type@8][body]</c> 的拆帧配置。
    /// <para>
    /// 长度字段只计帧体，整帧长度为 <c>9 + bodyLength</c>，因此修正值取 <c>+1</c>；
    /// 产出帧保留完整头部（<c>initialBytesToStrip = 0</c>），解码器直接按帧读类型与帧体。
    /// </para>
    /// </summary>
    public static class FileFrameSplitter
    {
        /// <summary>
        /// 创建文件传输分帧器：<c>0 / 8 / +1 / 0</c>，小端。
        /// </summary>
        /// <param name="maxFrameLength">帧体长度上限（字节），非负</param>
        /// <returns>无状态的拆帧器实例，可按连接复用</returns>
        public static LengthFieldSplitter Create(int maxFrameLength)
        {
            return new LengthFieldSplitter(
                maxFrameLength,
                0,
                FrameConstants.P_LEN,
                FrameConstants.P_Head - FrameConstants.P_LEN,
                0,
                true);
        }

        /// <summary>
        /// 取帧体长度上限：沿用 <see cref="BaseCoder.MaxFrameLength"/> 的可配置口径，
        /// 并按与既有 <c>FrameDecoder</c> 相同的规则夹逼，避免整帧长度在 int 运算中溢出。
        /// </summary>
        /// <returns>本次连接使用的帧体长度上限（字节）</returns>
        public static int ResolveMaxFrameLength()
        {
            var configured = BaseCoder.MaxFrameLength;

            return configured < FrameConstants.P_Head
                ? int.MaxValue - FrameConstants.P_Head
                : Math.Min(configured, int.MaxValue - FrameConstants.P_Head);
        }
    }
}