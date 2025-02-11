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
using System.Collections.Generic;

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
        private List<byte> _buffer = new List<byte>();

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
            _buffer.AddRange(data);

            // 循环处理缓冲区中的数据
            while (_buffer.Count >= P_Head)
            {
                // 将缓冲区数据转换为数组
                var buffer = _buffer.ToArray();

                // 获取数据包的长度
                var bodyLen = GetLength(buffer);

                // 获取数据包的类型
                var type = GetType(buffer);

                // 如果数据包长度为0且类型为心跳包，则处理心跳包
                if (bodyLen == 0 && type == SocketProtocalType.Heart) //空包认为是心跳包
                {
                    // 创建一个基础协议对象，设置长度和类型
                    var sm = new BaseSocketProtocal() { BodyLength = bodyLen, Type = (byte)type };
                    // 清空缓冲区
                    _buffer.Clear();
                    // 调用心跳回调函数
                    onHeart?.Invoke(DateTimeHelper.Now);
                }
                // 如果缓冲区数据长度足够解析一个完整的数据包
                else if (buffer.Length >= P_Head + bodyLen)
                {
                    // 如果数据包类型为大数据包
                    if (type == SocketProtocalType.BigData)
                    {
                        // 获取数据包内容
                        var content = GetContent(buffer, P_Head, (int)bodyLen);
                        // 移除已处理的数据
                        _buffer.RemoveRange(0, (int)(P_Head + bodyLen));
                        // 设置长度为0，表示已处理完
                        bodyLen = 0;
                        // 调用文件回调函数
                        onFile?.Invoke(content);
                    }
                    else
                    {
                        // 创建一个基础协议对象，设置长度、类型和内容
                        var sm = new BaseSocketProtocal() { BodyLength = bodyLen, Type = (byte)type, Content = GetContent(buffer, P_Head, (int)bodyLen) };
                        // 移除已处理的数据
                        _buffer.RemoveRange(0, (int)(P_Head + bodyLen));
                        // 设置长度为0，表示已处理完
                        bodyLen = 0;
                        // 调用解包回调函数
                        result.Add(sm);
                    }
                }
                else
                {
                    // 如果缓冲区数据长度不足以解析一个完整的数据包，则退出循环
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// 静态方法 GetLength，从字节数组中获取数据包长度
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static long GetLength(byte[] data)
        {
            return data.ToLong();
        }

        /// <summary>
        /// 静态方法 GetType，从字节数组中获取数据包类型
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static SocketProtocalType GetType(byte[] data)
        {
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
            var buffer = new byte[count];
            Buffer.BlockCopy(data, offset, buffer, 0, count);
            return buffer;
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _buffer?.Clear();
            _buffer = null;
        }
    }
}
