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
*文件名： BaseCoder
*版本号： v26.4.23.1
*唯一标识：ce7d5102-3f6b-483b-8299-1c91a2ae53d5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：BaseCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2025/02/10 17:07:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BaseCoder编解码类
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 定义一个基类 BaseCoder，实现 ICoder 接口
    /// </summary>
    public class BaseCoder : ICoder
    {
        // 定义常量 P_LEN，表示协议长度字段的偏移量
        public const int P_LEN = 8;

        // 定义常量 P_Type，表示协议类型字段的偏移量
        public const int P_Type = 1;

        // 定义常量 P_Head，表示协议头部长度
        public const int P_Head = 9;

        // 定义常量 SmallDataThreshold，小数据阈值（4KB）
        public const int SmallDataThreshold = 4 * 1024;

        /// <summary>
        /// 单帧最大帧体长度，可由部署方收紧
        /// </summary>
        public static int MaxFrameLength { get; set; } = int.MaxValue - P_Head;

        // 增量式拆帧内核
        private FrameDecoder _decoder = new FrameDecoder(MaxFrameLength);

        /// <summary>
        /// 内部委托：接收数据时触发（Span版本）
        /// </summary>
        /// <param name="data">数据Span</param>
        internal delegate void OnReceiveSpanHandler(ReadOnlySpan<byte> data);

        /// <summary>
        /// 内部事件：接收数据时触发（Span版本）
        /// </summary>
        internal event OnReceiveSpanHandler OnReceiveSpan;

        // 实现接口方法 Encode，将协议对象转换为字节数组
        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        /// <summary>
        /// 实现接口方法 Decode，解析接收到的字节数据（Span版本）
        /// </summary>
        /// <param name="data">数据Span</param>
        /// <param name="onHeart">心跳回调</param>
        /// <param name="onFile">文件回调</param>
        public List<ISocketProtocal> Decode(ReadOnlySpan<byte> data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            OnReceiveSpan?.Invoke(data);

            var result = new List<ISocketProtocal>();

            _decoder.Append(data);

            while (_decoder.TryReadFrame(out var kind, out var frame, out var fileContent, out var heartAt))
            {
                switch (kind)
                {
                    case FrameKind.Heart:
                        onHeart?.Invoke(heartAt);
                        break;
                    case FrameKind.File:
                        onFile?.Invoke(fileContent);
                        break;
                    case FrameKind.Data:
                        var content = frame.Content.Length == 0 ? Array.Empty<byte>() : frame.Content.ToArray();
                        result.Add(new BaseSocketProtocal() { BodyLength = frame.BodyLength, Type = frame.Type, Content = content });
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// 零拷贝流式解码：帧体以切片交付，仅在 handler 回调期间有效。
        /// </summary>
        public void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            OnReceiveSpan?.Invoke(data);

            _decoder.Append(data);

            while (_decoder.TryReadFrame(out var kind, out var frame, out var fileContent, out var heartAt))
            {
                switch (kind)
                {
                    case FrameKind.Heart:
                        onHeart?.Invoke(heartAt);
                        break;
                    case FrameKind.File:
                        onFile?.Invoke(fileContent);
                        break;
                    case FrameKind.Data:
                        handler.OnFrame(in frame);
                        break;
                }
            }
        }

        /// <summary>
        /// 实现接口方法 Decode，解析接收到的字节数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="onHeart"></param>
        /// <param name="onFile"></param>
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            // 委托给Span版本的方法
            return Decode(data.AsSpan(), onHeart, onFile);
        }

        /// <summary>
        /// 静态方法 GetLength，从字节数组中获取数据包长度
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static long GetLength(byte[] data)
        {
            if (data == null || data.Length < P_LEN)
                throw new ArgumentException("数据长度不足");
                
            return data.ToLong();
        }

        /// <summary>
        /// 静态方法 GetType，从字节数组中获取数据包类型
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static SocketProtocalType GetType(byte[] data)
        {
            if (data == null || data.Length < P_LEN + 1)
                throw new ArgumentException("数据长度不足");
                
            return (SocketProtocalType)data[P_LEN];
        }

        /// <summary>
        /// 静态方法 GetContent，从字节数组中获取数据包内容
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static byte[] GetContent(byte[] data, int offset, int count)
        {
            if (data == null || offset < 0 || count < 0 || offset + count > data.Length)
                throw new ArgumentException("参数无效");
                
            var buffer = new byte[count];
            Buffer.BlockCopy(data, offset, buffer, 0, count);
            return buffer;
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _decoder?.Clear();
        }
    }
}