/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
 *Copyright (c)  yswenli All Rights Reserved.
 *CLR版本： 2.1.4
 *机器名称：WENLI-PC
 *公司名称：wenli
 *命名空间：SAEA.Sockets.Model
 *文件名： BaseUnpacker
 *版本号： v7.0.0.1
 *唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：wenguoli_520@qq.com
 *修改时间：2018/10/26 15:54:21
 *描述：
 *
 *=====================================================================
 *修改标记
 *修改时间：2018/10/26 15:54:21
 *修改人： yswenli
 *版本号： v7.0.0.1
 *描述：
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

        // 定义一个私有字段 _buffer，用于存储接收到的数据
        private MemoryStream _buffer = new MemoryStream();

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

            _buffer.Position = _buffer.Length;
            _buffer.Write(data.ToArray(), 0, data.Length);

            _buffer.Position = 0;

            while (_buffer.Length - _buffer.Position >= P_Head)
            {
                _buffer.Position = 0;

                var bodyLen = ReadLengthFromBuffer();

                _buffer.Position = P_LEN;

                var type = (SocketProtocalType)_buffer.ReadByte();

                _buffer.Position = 0;

                if (bodyLen == 0 && type == SocketProtocalType.Heart)
                {
                    var sm = new BaseSocketProtocal() { BodyLength = bodyLen, Type = (byte)type };
                    RemoveFromBuffer(P_Head);
                    onHeart?.Invoke(DateTimeHelper.Now);
                }
                else if (_buffer.Length >= P_Head + bodyLen)
                {
                    if (type == SocketProtocalType.BigData)
                    {
                        var content = ReadContentFromBufferSpan(P_Head, (int)bodyLen);
                        RemoveFromBuffer((int)(P_Head + bodyLen));
                        onFile?.Invoke(content);
                    }
                    else
                    {
                        var content = ReadContentFromBufferSpan(P_Head, (int)bodyLen);
                        var sm = new BaseSocketProtocal() { BodyLength = bodyLen, Type = (byte)type, Content = content };
                        RemoveFromBuffer((int)(P_Head + bodyLen));
                        result.Add(sm);
                    }
                }
                else
                {
                    _buffer.Position = _buffer.Length;
                    break;
                }
            }
            return result;
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
        /// 从缓冲区读取长度信息
        /// </summary>
        /// <returns></returns>
        private long ReadLengthFromBuffer()
        {
            byte[] lenBytes = ArrayPool<byte>.Shared.Rent(P_LEN);
            try
            {
                _buffer.Read(lenBytes, 0, P_LEN);
                return lenBytes.ToLong();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lenBytes);
            }
        }

        /// <summary>
        /// 从缓冲区读取内容（Span优化版本）
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private byte[] ReadContentFromBufferSpan(int offset, int count)
        {
            if (count <= 0) return Array.Empty<byte>();

            // 根据数据大小选择分配策略
            byte[] result;
            if (count < SmallDataThreshold)
            {
                // 小数据：直接分配
                result = new byte[count];
            }
            else
            {
                // 大数据：从内存池租用
                result = MemoryPoolManager.Rent(count);
            }

            // 移动到指定位置
            _buffer.Position = offset;

            // 读取数据
            _buffer.Read(result, 0, count);

            // 重置位置到开始
            _buffer.Position = 0;

            return result;
        }

        /// <summary>
        /// 从缓冲区读取内容（保持兼容性的原始版本，内部委托给Span版本）
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private byte[] ReadContentFromBuffer(int offset, int count)
        {
            return ReadContentFromBufferSpan(offset, count);
        }

        /// <summary>
        /// 从缓冲区移除指定长度的数据
        /// </summary>
        /// <param name="length"></param>
        private void RemoveFromBuffer(int length)
        {
            long remaining = _buffer.Length - length;
            if (remaining <= 0)
            {
                Clear();
                return;
            }
            
            // 创建临时缓冲区保存剩余数据
            byte[] tempBuffer = ArrayPool<byte>.Shared.Rent((int)remaining);
            try
            {
                // 移动到已处理数据之后
                _buffer.Position = length;
                
                // 读取剩余数据
                _buffer.Read(tempBuffer, 0, (int)remaining);
                
                // 清空并重置流
                _buffer.SetLength(0);
                _buffer.Position = 0;
                
                // 写入剩余数据
                _buffer.Write(tempBuffer, 0, (int)remaining);
                _buffer.Position = 0;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
            }
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
            _buffer?.SetLength(0);
            _buffer?.Seek(0, SeekOrigin.Begin);
        }
    }
}
