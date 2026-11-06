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
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
        /// <summary>
        /// 帧体总长度（字节），大数据帧下可大于 Content.Length
        /// </summary>
        public long BodyLength { get; protected set; }
        /// <summary>
        /// 帧类型，取值见 <see cref="SocketProtocalType"/>
        /// </summary>
        public byte Type { get; protected set; }
        /// <summary>
        /// 帧体内容，未设置时为空
        /// </summary>
        public ReadOnlyMemory<byte> Content { get; protected set; } = ReadOnlyMemory<byte>.Empty;

        /// <summary>
        /// 创建空协议对象：帧长为 0、帧体为空，供派生类自行填充
        /// </summary>
        public BaseSocketProtocal() { }

        /// <summary>
        /// 以单块帧体创建协议对象，帧长取帧体实际长度
        /// </summary>
        /// <param name="type">帧类型</param>
        /// <param name="content">帧体内容</param>
        public BaseSocketProtocal(byte type, ReadOnlyMemory<byte> content)
        {
            Type = type;
            Content = content;
            BodyLength = content.Length;
        }

        /// <summary>
        /// 以显式帧长创建协议对象：BodyLength 可大于 Content.Length（如 BigData 帧只携带分块），基类不做一致性校验
        /// </summary>
        /// <param name="bodyLength">帧体总长度（字节）</param>
        /// <param name="type">帧类型</param>
        /// <param name="content">本条消息携带的帧体内容</param>
        public BaseSocketProtocal(long bodyLength, byte type, ReadOnlyMemory<byte> content)
        {
            BodyLength = bodyLength;
            Type = type;
            Content = content;
        }

        /// <summary>
        /// 按线格式写入 writer：8 字节小端帧长 + 1 字节类型，帧体为空时只写帧头
        /// </summary>
        /// <param name="writer">目标缓冲写入器</param>
        public void WriteTo(IBufferWriter<byte> writer)
        {
            var span = writer.GetSpan(BaseCoder.P_Head);
            BinaryPrimitives.WriteInt64LittleEndian(span, BodyLength);
            span[BaseCoder.P_LEN] = Type;
            writer.Advance(BaseCoder.P_Head);
            if (!Content.IsEmpty)
                writer.Write(Content.Span);
        }

        /// <summary>
        /// 用整块数据构造协议对象：帧长取数据长度（null 视为 0），帧体直接引用入参数组、不复制
        /// </summary>
        /// <param name="data">帧体数据，可为 null</param>
        /// <param name="type">帧类型</param>
        /// <returns>新协议对象</returns>
        public static BaseSocketProtocal Parse(byte[] data, SocketProtocalType type)
        {
            return Parse(data, (byte)type);
        }

        /// <summary>
        /// 用整块数据构造协议对象：帧长取数据长度（null 视为 0），帧体直接引用入参数组、不复制
        /// </summary>
        /// <param name="data">帧体数据，可为 null</param>
        /// <param name="type">帧类型字节值</param>
        /// <returns>新协议对象</returns>
        public static BaseSocketProtocal Parse(byte[] data, byte type)
        {
            var len = data == null ? 0 : data.Length;
            return new BaseSocketProtocal(len, type, len > 0 ? new ReadOnlyMemory<byte>(data) : ReadOnlyMemory<byte>.Empty);
        }

        /// <summary>
        /// 构造类型为 RequestSend 的整包协议对象
        /// </summary>
        /// <param name="data">帧体数据，可为 null</param>
        /// <returns>新协议对象</returns>
        public static BaseSocketProtocal ParseRequest(byte[] data)
        {
            return Parse(data, SocketProtocalType.RequestSend);
        }

        /// <summary>
        /// 构造类型为 BigData 的整包协议对象，解码端按文件帧经 onFile 回调交出
        /// </summary>
        /// <param name="data">帧体数据，可为 null</param>
        /// <returns>新协议对象</returns>
        public static BaseSocketProtocal ParseStream(byte[] data)
        {
            var len = data == null ? 0 : data.Length;
            return new BaseSocketProtocal(len, (byte)SocketProtocalType.BigData, len > 0 ? new ReadOnlyMemory<byte>(data) : ReadOnlyMemory<byte>.Empty);
        }
    }
}