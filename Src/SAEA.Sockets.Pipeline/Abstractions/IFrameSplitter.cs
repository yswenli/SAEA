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
*文件名： IFrameSplitter
*版本号： v26.11.5.1
*唯一标识：ef7b850f-7d19-472a-bdc4-3e123a44c017
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第一层分帧：从连续缓冲中切出完整帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:45:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：分帧输入改为 ReadOnlySequence，支持泵直接切帧与跨段帧
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第一层分帧：从尚未消费的缓冲里切出完整帧，缓冲可能跨多段。实现不得产生协议元数据，
    /// 只按配置决定是否剥掉包头。
    /// </summary>
    public interface IFrameSplitter
    {
        /// <summary>
        /// 从当前未消费缓冲里切出下一帧。返回 false 表示数据不足，需要继续读取，
        /// 此时 <paramref name="consumed"/> 与 <paramref name="frame"/> 的值无效。
        /// </summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲，可能跨多段。</param>
        /// <param name="consumed">本次应从缓冲中消费掉的字节数（含被剥离的包头），成功时必须大于 0 且不超过缓冲长度。</param>
        /// <param name="frame">产出的帧切片，可能跨段；只在 <paramref name="buffer"/> 仍然有效期内可读，调用方须在消费该缓冲前复制成独立背衬，实现也不得保存该切片。</param>
        /// <returns>true 表示切出完整帧；false 表示数据不足，需要继续读取。</returns>
        bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame);

        /// <summary>
        /// 可接受的最大帧长。按帧体的口径解释；超限抛
        /// <see cref="FrameProtocolException"/>，用于防止内存被超大帧耗尽。
        /// </summary>
        int MaxFrameLength { get; }

        /// <summary>连接断开或握手失败时清空内部状态。</summary>
        void Reset();
    }
}