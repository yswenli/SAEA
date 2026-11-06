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
*命名空间：SAEA.FTP.Net
*文件名： FTPCoder
*版本号： v26.4.23.1
*唯一标识：cafc8022-b13d-40f5-9afc-b724ca2c40a9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：FTPCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Sockets.Interface;

using System;
using System.Collections.Generic;

namespace SAEA.FTP.Net
{
    /// <summary>
    /// FTP 协议编解码器：编码直接写出协议对象，解码未实现
    /// </summary>
    public class FTPCoder : ICoder
    {

        /// <summary>
        /// 把协议对象写入目标缓冲区
        /// </summary>
        /// <param name="protocal">待写出的协议对象</param>
        /// <param name="writer">目标缓冲区写入器</param>
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            protocal.WriteTo(writer);
        }

        /// <summary>
        /// 解码入口，当前为空实现，不解析任何数据
        /// </summary>
        /// <param name="data">待解码数据</param>
        /// <param name="unpackCallback">解包完成回调</param>
        /// <param name="onHeart">心跳回调</param>
        /// <param name="onFile">文件数据回调</param>
        public void Decode(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }

        /// <summary>
        /// 清理解码缓存，未实现，调用即抛 <see cref="NotImplementedException"/>
        /// </summary>
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 按序解码数据帧，未实现，调用即抛 <see cref="NotImplementedException"/>
        /// </summary>
        /// <param name="data">待解码的数据序列</param>
        /// <param name="onHeart">心跳回调</param>
        /// <param name="onFile">文件数据回调</param>
        /// <returns>解码出的帧集合，本实现不会返回</returns>
        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data,
            Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            throw new NotImplementedException();
        }
    }
}
