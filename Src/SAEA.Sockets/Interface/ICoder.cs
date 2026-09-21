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
*文件名： ICoder
*版本号： v26.4.23.1
*唯一标识：d3d27658-36c1-42c4-89eb-f4f9b544091e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ICoder接口
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ICoder接口
*
*****************************************************************************/
using System;
using System.Buffers;
using SAEA.Sockets.Base;

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 通信数据编解码器。
    /// </summary>
    public interface ICoder
    {
        /// <summary>编码协议对象，写入 writer。</summary>
        void Encode(ISocketProtocal protocal, IBufferWriter<byte> writer);

        /// <summary>
        /// 有状态解码（复用半包缓存）。多段序列按段喂入。
        /// 返回的 <see cref="DecodedFrames"/> 必须 using。
        /// </summary>
        DecodedFrames Decode(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null);

        /// <summary>清除内部状态。</summary>
        void Clear();
    }
}