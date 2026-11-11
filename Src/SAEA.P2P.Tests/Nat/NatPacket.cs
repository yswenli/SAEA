/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Nat
*文件名： NatPacket
*版本号： net10.0
*唯一标识：01815eef-b7ba-4564-b884-563ef4d47570
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:40:00
*描述：内部主机与 NAT 网关之间的地址头编解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 内部通道地址头编解码
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 内部主机与 NAT 网关之间的报文格式：4 字节 IPv4 地址 + 2 字节大端端口 + 原始载荷。
    /// 出向头写目标端点，回程头写真实来源端点；仅支持 IPv4，NAT66 不在本轮验收范围。
    /// </summary>
    public static class NatPacket
    {
        /// <summary>
        /// 地址头长度
        /// </summary>
        public const int HeaderLength = 6;

        /// <summary>
        /// 组装带地址头的报文
        /// </summary>
        /// <param name="endPoint">出向写目标端点，回程写来源端点</param>
        /// <param name="payload">原始载荷，可为空</param>
        /// <returns>可直接交给 UDP 发送的字节数组</returns>
        public static byte[] Encode(IPEndPoint endPoint, ReadOnlySpan<byte> payload)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            var address = endPoint.Address.MapToIPv4().GetAddressBytes();
            var packet = new byte[HeaderLength + payload.Length];

            Buffer.BlockCopy(address, 0, packet, 0, 4);
            packet[4] = (byte)(endPoint.Port >> 8);
            packet[5] = (byte)endPoint.Port;
            payload.CopyTo(packet.AsSpan(HeaderLength));

            return packet;
        }

        /// <summary>
        /// 解析带地址头的报文
        /// </summary>
        /// <param name="buffer">接收缓冲</param>
        /// <param name="offset">有效数据起始位置</param>
        /// <param name="count">有效数据长度</param>
        /// <param name="endPoint">解析出的端点</param>
        /// <param name="payload">载荷切片，指向原缓冲</param>
        /// <returns>长度足够且地址合法时为 true</returns>
        public static bool TryDecode(byte[] buffer, int offset, int count, out IPEndPoint endPoint, out ArraySegment<byte> payload)
        {
            endPoint = null;
            payload = default;

            if (buffer == null || count < HeaderLength) return false;
            if (offset < 0 || offset + count > buffer.Length) return false;

            var address = new byte[4];
            Buffer.BlockCopy(buffer, offset, address, 0, 4);
            var port = (buffer[offset + 4] << 8) | buffer[offset + 5];

            endPoint = new IPEndPoint(new IPAddress(address), port);
            payload = new ArraySegment<byte>(buffer, offset + HeaderLength, count - HeaderLength);
            return true;
        }
    }
}