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
*文件名： ICodec
*版本号： v26.11.5.1
*唯一标识：4c27cbf5-0857-434c-b5d9-89520771c837
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第一层编解码：帧字节与信封之间转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：第一层编解码：帧字节与信封之间转换
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第一层编解码：帧字节与信封之间转换。分帧器产出的帧交给它。
    /// </summary>
    public interface ICodec
    {
        /// <summary>
        /// 把帧解码为信封对象。入参是分帧器产出的帧切片，
        /// 其偏移由分帧配置决定，实现不得自带包头偏移假设。
        /// 实现不得保留该内存：返回后调用方会立即释放其背衬，
        /// 需要留存的部分必须复制到独立内存。
        /// </summary>
        /// <param name="frame">分帧器产出的帧切片，仅在本次调用期间有效。</param>
        /// <returns>解码得到的信封对象，所有权交给下游处理器。</returns>
        object Decode(ReadOnlyMemory<byte> frame);

        /// <summary>把消息编码为 body，只写 body，不写包头。</summary>
        /// <param name="message">待编码的信封或业务对象。</param>
        /// <param name="writer">body 的输出目标，由调用方提供背衬。</param>
        void Encode(object message, IBufferWriter<byte> writer);
    }
}