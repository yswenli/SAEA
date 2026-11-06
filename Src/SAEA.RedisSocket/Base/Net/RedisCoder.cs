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
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RedisCoder
*版本号： v26.4.23.1
*唯一标识：9091f8bb-8eea-4d9b-a41f-f357027cf6e8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：RedisCoder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Sockets.Interface;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// 通信数据接收解析器
    /// </summary>
    public sealed class RedisCoder : ICoder
    {
        /// <summary>
        /// 发送编码，当前未实现并直接抛出 NotImplementedException
        /// </summary>
        /// <param name="protocal">待编码的协议对象</param>
        /// <param name="writer">目标缓冲区写入器</param>
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 收包处理，当前未实现并直接抛出 NotImplementedException
        /// </summary>
        /// <param name="data">接收到的原始字节序列</param>
        /// <param name="onHeart">心跳回调，可为 null</param>
        /// <param name="onFile">文件数据回调，可为 null</param>
        /// <returns>解码后的帧集合，当前实现不返回</returns>
        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 清空解析状态，当前为空实现
        /// </summary>
        public void Clear()
        {

        }
    }
}