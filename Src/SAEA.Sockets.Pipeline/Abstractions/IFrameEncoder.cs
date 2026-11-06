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
*命名空间：SAEA.Sockets.Pipeline
*文件名： IFrameEncoder
*版本号： v26.11.5.1
*唯一标识：df362684-ab97-4b4b-8b40-bc2f362ab40c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站分帧：把 body 写成带包头的完整帧，与 IFrameSplitter 互逆
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站分帧：把 body 写成带包头的完整帧，与 IFrameSplitter 互逆
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站分帧：把 body 写成带包头的完整帧，与 <see cref="IFrameSplitter"/> 互逆。
    /// </summary>
    public interface IFrameEncoder
    {
        /// <summary>
        /// 把 body 写成完整帧。只写 body 与协议头，不做序列化。
        /// <paramref name="typeCode"/> 必须落在 0..255，越界抛
        /// <see cref="ArgumentOutOfRangeException"/>，不得静默截断。
        /// </summary>
        void Encode(ReadOnlyMemory<byte> body, int typeCode, IBufferWriter<byte> writer);
    }
}