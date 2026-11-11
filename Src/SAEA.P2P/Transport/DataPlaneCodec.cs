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
*命名空间：SAEA.P2P.Transport
*文件名： DataPlaneCodec
*版本号： v26.11.5.1
*唯一标识：df3b3e22-212e-41bd-831d-8ed6ed0c654f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 19:00:00
*描述：数据面业务帧前缀与 UDP 首字节解复用
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 19:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增数据面业务帧前缀与 UDP 首字节解复用
*
*****************************************************************************/
using System;
using SAEA.P2P.Common;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 数据报首字节解复用后的类别
    /// </summary>
    public enum DataPlaneKind
    {
        /// <summary>
        /// 空数据报或首字节不属于任何已分配区间
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// STUN 报文，首字节位于 0x00 到 0x3F
        /// </summary>
        Stun = 1,

        /// <summary>
        /// 业务帧，首字节为 0xC0
        /// </summary>
        Data = 2
    }

    /// <summary>
    /// 数据面业务帧编解码：统一 1 字节前缀 0xC0，并与 STUN 抢占同一数据报时按首字节区分。
    /// 只依赖 System 命名空间，不引用 SAEA.Sockets 类型。
    /// </summary>
    public static class DataPlaneCodec
    {
        /// <summary>
        /// 业务帧前缀字节，落在 RFC 7983 未分配区间
        /// </summary>
        public const byte DataPrefix = 0xC0;

        /// <summary>
        /// UDP 单包长度上限，与 SAEA.Sockets 的 SocketOption.UDPMaxLength 保持同值；不直接引用该类型，保持核心路径只依赖 System.Net
        /// </summary>
        public const int MaxUdpDatagramLength = 65506;

        /// <summary>
        /// 单帧业务载荷上限，即 UDP 单包上限扣除 1 字节前缀
        /// </summary>
        public const int MaxPayloadLength = MaxUdpDatagramLength - 1;

        /// <summary>
        /// 按首字节判定数据报类别：空包与未知首字节归为 Unknown
        /// </summary>
        /// <param name="datagram">收到的 UDP 数据报</param>
        /// <returns>数据报类别</returns>
        public static DataPlaneKind Classify(ReadOnlySpan<byte> datagram)
        {
            if (datagram.Length == 0)
            {
                return DataPlaneKind.Unknown;
            }

            var first = datagram[0];
            if (first == DataPrefix)
            {
                return DataPlaneKind.Data;
            }

            if (first <= 0x3F)
            {
                return DataPlaneKind.Stun;
            }

            return DataPlaneKind.Unknown;
        }

        /// <summary>
        /// 给业务载荷加上 0xC0 前缀，返回可独立持有的一帧
        /// </summary>
        /// <param name="payload">调用方原始负载，长度不得超过 <see cref="MaxPayloadLength"/></param>
        /// <returns>新数组，首字节为 0xC0，其后是 payload 的副本</returns>
        /// <exception cref="P2PException">载荷超过单包上限时抛出，错误码为 PayloadTooLarge</exception>
        public static byte[] Wrap(ReadOnlySpan<byte> payload)
        {
            if (payload.Length > MaxPayloadLength)
            {
                throw new P2PException(ErrorCode.PayloadTooLarge);
            }

            var frame = new byte[payload.Length + 1];
            frame[0] = DataPrefix;
            payload.CopyTo(frame.AsSpan(1));
            return frame;
        }

        /// <summary>
        /// 校验 0xC0 前缀并切出业务载荷
        /// </summary>
        /// <param name="datagram">收到的 UDP 数据报</param>
        /// <param name="payload">成功时为去掉前缀的载荷视图，指向 datagram 的同一内存</param>
        /// <returns>首字节为 0xC0 时返回 true</returns>
        public static bool TryUnwrap(ReadOnlySpan<byte> datagram, out ReadOnlySpan<byte> payload)
        {
            if (datagram.Length == 0 || datagram[0] != DataPrefix)
            {
                payload = default(ReadOnlySpan<byte>);
                return false;
            }

            payload = datagram.Slice(1);
            return true;
        }
    }
}