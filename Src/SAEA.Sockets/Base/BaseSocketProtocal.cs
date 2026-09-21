/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets.Base
*文件名： BaseSocketProtocal
*版本号： v26.4.23.1
*唯一标识：0570ccc4-8c29-4122-adf7-8ffb78ed53d8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/21 19:42:03
*描述：BaseSocketProtocal类
*
*=====================================================================
*修改标记
*修改时间：2019/08/21 19:42:03
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BaseSocketProtocal类
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 系统默认消息协议（非 sealed：P2PProtocol 等派生）。
    /// </summary>
    public class BaseSocketProtocal : ISocketProtocal
    {
        public long BodyLength { get; protected set; }
        public byte Type { get; protected set; }
        public ReadOnlyMemory<byte> Content { get; protected set; } = ReadOnlyMemory<byte>.Empty;

        public BaseSocketProtocal() { }

        public BaseSocketProtocal(byte type, ReadOnlyMemory<byte> content)
        {
            Type = type;
            Content = content;
            BodyLength = content.Length;
        }

        public BaseSocketProtocal(long bodyLength, byte type, ReadOnlyMemory<byte> content)
        {
            BodyLength = bodyLength;
            Type = type;
            Content = content;
        }

        public void WriteTo(IBufferWriter<byte> writer)
        {
            var span = writer.GetSpan(BaseCoder.P_Head);
            BinaryPrimitives.WriteInt64LittleEndian(span, BodyLength);
            span[BaseCoder.P_LEN] = Type;
            writer.Advance(BaseCoder.P_Head);
            if (!Content.IsEmpty)
                writer.Write(Content.Span);
        }

        public static BaseSocketProtocal Parse(byte[] data, SocketProtocalType type)
        {
            return Parse(data, (byte)type);
        }

        public static BaseSocketProtocal Parse(byte[] data, byte type)
        {
            var len = data == null ? 0 : data.Length;
            return new BaseSocketProtocal(len, type, len > 0 ? new ReadOnlyMemory<byte>(data) : ReadOnlyMemory<byte>.Empty);
        }

        public static BaseSocketProtocal ParseRequest(byte[] data)
        {
            return Parse(data, SocketProtocalType.RequestSend);
        }

        public static BaseSocketProtocal ParseStream(byte[] data)
        {
            var len = data == null ? 0 : data.Length;
            return new BaseSocketProtocal(len, (byte)SocketProtocalType.BigData, len > 0 ? new ReadOnlyMemory<byte>(data) : ReadOnlyMemory<byte>.Empty);
        }
    }
}