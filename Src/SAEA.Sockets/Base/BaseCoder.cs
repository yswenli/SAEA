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

        // 定义一个私有字段 _buffer，用于存储接收到的数据
        private MemoryStream _buffer = new MemoryStream();

        // 实现接口方法 Encode，将协议对象转换为字节数组
        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        /// <summary>
        /// 实现接口方法 Decode，解析接收到的字节数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="onHeart"></param>
        /// <param name="onFile"></param>
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            var result = new List<ISocketProtocal>();

            // 将接收到的数据添加到缓冲区
            _buffer.Write(data, 0, data.Length);
            
            // 重置流位置到开始
            _buffer.Position = 0;

            // 循环处理缓冲区中的数据
            while (_buffer.Length - _buffer.Position >= P_Head)
            {
                // 获取数据包的长度
                var bodyLen = ReadLengthFromBuffer();
                
                // 重置位置到类型字段
                _buffer.Position = P_LEN;
                
                // 获取数据包的类型
                var type = (SocketProtocalType)_buffer.ReadByte();
                
                // 重置位置到开始
                _buffer.Position = 0;

                // 如果数据包长度为0且类型为心跳包，则处理心跳包
                if (bodyLen == 0 && type == SocketProtocalType.Heart) //空包认为是心跳包
                {
                    // 创建一个基础协议对象，设置长度和类型
                    var sm = new BaseSocketProtocal() { BodyLength = bodyLen, Type = (byte)type };
                    // 清空缓冲区
                    Clear();
                    // 调用心跳回调函数
                    onHeart?.Invoke(DateTimeHelper.Now);
                }
                // 如果缓冲区数据长度足够解析一个完整的数据包
                else if (_buffer.Length >= P_Head + bodyLen)
                {
                    // 如果数据包类型为大数据包
                    if (type == SocketProtocalType.BigData)
                    {
                        // 获取数据包内容
                        var content = ReadContentFromBuffer(P_Head, (int)bodyLen);
                        // 移除已处理的数据
                        RemoveFromBuffer((int)(P_Head + bodyLen));
                        // 调用文件回调函数
                        onFile?.Invoke(content);
                    }
                    else
                    {
                        // 创建一个基础协议对象，设置长度、类型和内容
                        var sm = new BaseSocketProtocal() { BodyLength = bodyLen, Type = (byte)type, Content = ReadContentFromBuffer(P_Head, (int)bodyLen) };
                        // 移除已处理的数据
                        RemoveFromBuffer((int)(P_Head + bodyLen));
                        // 调用解包回调函数
                        result.Add(sm);
                    }
                }
                else
                {
                    // 如果缓冲区数据长度不足以解析一个完整的数据包，则退出循环
                    // 将位置重置到数据末尾，准备接收新数据
                    _buffer.Position = _buffer.Length;
                    break;
                }
            }
            return result;
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
        /// 从缓冲区读取内容
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private byte[] ReadContentFromBuffer(int offset, int count)
        {
            if (count <= 0) return Array.Empty<byte>();
            
            // 移动到指定位置
            _buffer.Position = offset;
            
            var content = new byte[count];
            _buffer.Read(content, 0, count);
            
            // 重置位置到开始
            _buffer.Position = 0;
            
            return content;
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
