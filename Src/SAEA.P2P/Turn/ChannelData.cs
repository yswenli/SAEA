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
*命名空间：SAEA.P2P.Turn
*文件名： ChannelData
*版本号： v26.11.5.1
*唯一标识：3304b81d-2b4c-45f0-bc45-dc4556a73336
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 22:30:00
*描述：TURN ChannelData 报文编解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 22:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ChannelData 报文编解码
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN ChannelData 报文：2 字节通道号加 2 字节长度，长度只计应用数据、不含头部与补齐字节。
    /// 通道号只接受 0x4000 到 0x4FFF；其它取值属于 RFC 7983 的复用区间，收到后静默丢弃。
    /// </summary>
    public static class ChannelData
    {
        /// <summary>
        /// 固定头部长度，单位字节
        /// </summary>
        public const int HeaderLength = 4;

        /// <summary>
        /// 可用通道号下界
        /// </summary>
        public const ushort MinChannelNumber = 0x4000;

        /// <summary>
        /// 可用通道号上界
        /// </summary>
        public const ushort MaxChannelNumber = 0x4FFF;

        /// <summary>
        /// 判断通道号是否落在 0x4000 到 0x4FFF 之间
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <returns>合法返回 true</returns>
        public static bool IsValidChannelNumber(ushort channelNumber)
        {
            return channelNumber >= MinChannelNumber && channelNumber <= MaxChannelNumber;
        }

        /// <summary>
        /// 编码一条 ChannelData 报文；TCP 与 TLS 上需要补齐到 4 字节倍数
        /// </summary>
        /// <param name="channelNumber">通道号，取值 0x4000 到 0x4FFF</param>
        /// <param name="payload">应用数据</param>
        /// <param name="padToWord">是否补齐到 4 字节倍数，TCP 与 TLS 上传 true</param>
        /// <returns>完整报文</returns>
        /// <exception cref="ArgumentOutOfRangeException">通道号不合法</exception>
        /// <exception cref="ArgumentException">应用数据长度超出 16 位长度字段</exception>
        public static byte[] Encode(ushort channelNumber, ReadOnlySpan<byte> payload, bool padToWord)
        {
            if (!IsValidChannelNumber(channelNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(channelNumber), "通道号必须落在 0x4000 到 0x4FFF 之间");
            }
            if (payload.Length > ushort.MaxValue)
            {
                throw new ArgumentException("应用数据长度超出 ChannelData 长度字段可表示范围", nameof(payload));
            }

            var bodyLength = padToWord ? ((payload.Length + 3) & ~3) : payload.Length;
            var buffer = new byte[HeaderLength + bodyLength];
            buffer[0] = (byte)(channelNumber >> 8);
            buffer[1] = (byte)channelNumber;

            // 长度字段只计应用数据，补齐字节不计入
            buffer[2] = (byte)(payload.Length >> 8);
            buffer[3] = (byte)payload.Length;
            payload.CopyTo(buffer.AsSpan(HeaderLength));
            return buffer;
        }

        /// <summary>
        /// 解析一条完整的 ChannelData 报文，按 UDP 语义处理：不要求补齐，尾部多余字节忽略
        /// </summary>
        /// <param name="data">完整报文</param>
        /// <param name="channelNumber">通道号</param>
        /// <param name="payload">应用数据切片，指向原缓冲</param>
        /// <returns>通道号合法且数据完整时返回 true</returns>
        public static bool TryDecode(ReadOnlySpan<byte> data, out ushort channelNumber, out ReadOnlySpan<byte> payload)
        {
            int consumed;
            return TryDecode(data, false, out channelNumber, out payload, out consumed);
        }

        /// <summary>
        /// 解析一条完整的 ChannelData 报文
        /// </summary>
        /// <param name="data">报文</param>
        /// <param name="padded">是否按 4 字节倍数补齐，TCP 与 TLS 上传 true</param>
        /// <param name="channelNumber">通道号</param>
        /// <param name="payload">应用数据切片，指向原缓冲</param>
        /// <param name="consumed">本条报文占用的字节数，含补齐字节</param>
        /// <returns>通道号合法且数据完整时返回 true</returns>
        public static bool TryDecode(ReadOnlySpan<byte> data, bool padded, out ushort channelNumber, out ReadOnlySpan<byte> payload, out int consumed)
        {
            channelNumber = 0;
            payload = ReadOnlySpan<byte>.Empty;
            consumed = 0;

            if (data.Length < HeaderLength)
            {
                return false;
            }

            var channel = (ushort)((data[0] << 8) | data[1]);
            if (!IsValidChannelNumber(channel))
            {
                return false;
            }

            var length = (data[2] << 8) | data[3];
            var total = HeaderLength + length;
            if (padded)
            {
                total = (total + 3) & ~3;
            }
            if (total > data.Length)
            {
                return false;
            }

            channelNumber = channel;
            payload = data.Slice(HeaderLength, length);
            consumed = total;
            return true;
        }

        /// <summary>
        /// 读取 ChannelData 头部声明的本条报文总长度，供流式解析判断是否已收全；
        /// 头部不足 4 字节或通道号非法时返回 false，调用方据此等待更多数据或丢弃
        /// </summary>
        /// <param name="data">已收到的字节</param>
        /// <param name="padded">是否按 4 字节倍数补齐，TCP 与 TLS 上传 true</param>
        /// <param name="totalLength">本条报文总长度，含头部与补齐字节</param>
        /// <returns>头部完整且通道号合法时返回 true</returns>
        public static bool TryGetTotalLength(ReadOnlySpan<byte> data, bool padded, out int totalLength)
        {
            totalLength = 0;
            if (data.Length < HeaderLength)
            {
                return false;
            }

            var channel = (ushort)((data[0] << 8) | data[1]);
            if (!IsValidChannelNumber(channel))
            {
                return false;
            }

            var length = (data[2] << 8) | data[3];
            totalLength = HeaderLength + length;
            if (padded)
            {
                totalLength = (totalLength + 3) & ~3;
            }
            return true;
        }
    }
}