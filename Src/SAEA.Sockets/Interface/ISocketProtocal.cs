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
*命名空间：SAEA.Sockets.Interface
*文件名： ISocketProtocal
*版本号： v26.4.23.1
*唯一标识：a7eb88c5-4023-476b-b784-74077d779547
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ISocketProtocal接口
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

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 协议对象：按 8B 小端长度 + 1B 类型 + body 的线格式描述一帧数据
    /// </summary>
    public interface ISocketProtocal
    {
        /// <summary>
        /// 协议头声明的 body 长度（字节）；大帧可大于 Content 实际长度
        /// </summary>
        long BodyLength { get; }

        /// <summary>
        /// 帧类型，取值见 SocketProtocalType
        /// </summary>
        byte Type { get; }

        /// <summary>
        /// body 内容；无 body 时为空，大帧只包含当前分块
        /// </summary>
        ReadOnlyMemory<byte> Content { get; }

        /// <summary>按线格式（8B 小端长度 + 1B Type + body）写入 writer。</summary>
        /// <param name="writer">目标缓冲区写入器，需能容纳 9 字节头 + body</param>
        void WriteTo(IBufferWriter<byte> writer);
    }
}