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
*命名空间：SAEA.Sockets.Base
*文件名： BaseCoder
*版本号： v26.4.23.1
*唯一标识：ce7d5102-3f6b-483b-8299-1c91a2ae53d5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：BaseCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2025/02/10 17:07:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BaseCoder编解码类
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 帧式编解码基类。
    /// </summary>
    public class BaseCoder : IFrameCoder
    {
        public const int P_LEN = 8;
        public const int P_Type = 1;
        public const int P_Head = 9;
        public const int SmallDataThreshold = 4 * 1024;

        /// <summary>单帧最大帧体长度，可由部署方收紧。构造 FrameDecoder 时捕获。</summary>
        public static int MaxFrameLength { get; set; } = int.MaxValue - P_Head;

        private FrameDecoder _decoder = new FrameDecoder(MaxFrameLength);

        public void Encode(ISocketProtocal protocal, IBufferWriter<byte> writer)
        {
            protocal.WriteTo(writer);
        }

        /// <summary>有状态批量解码。返回批次须 using。</summary>
        public DecodedFrames Decode(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            var collector = new FramesCollector();
            try
            {
                if (data.IsSingleSegment)
                {
                    DecodeStream(data.First.Span, collector, collector.AddHeart, collector.AddFile);
                }
                else
                {
                    foreach (var segment in data)
                        DecodeStream(segment.Span, collector, collector.AddHeart, collector.AddFile);
                }
                return collector.Build(onHeart, onFile);
            }
            finally
            {
                // Build 成功后 _pooled 已转移（Dispose 为 no-op）；异常路径（如非法帧 KernelException）在此归还缓冲。
                collector.Dispose();
            }
        }

        /// <summary>
        /// 有状态批量解码（Span 直投）。等价于 <see cref="Decode(ReadOnlySequence{byte}, Action{DateTime}, Action{ReadOnlyMemory{byte}})"/>，
        /// 但省去调用方把 span 先物化为数组的中间拷贝。返回批次须 using。
        /// </summary>
        public DecodedFrames DecodeSpan(ReadOnlySpan<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            var collector = new FramesCollector();
            try
            {
                DecodeStream(data, collector, collector.AddHeart, collector.AddFile);
                return collector.Build(onHeart, onFile);
            }
            finally
            {
                collector.Dispose();
            }
        }

        /// <summary>零拷贝流式解码：帧体仅在回调期间有效。</summary>
        public void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, FileSpanHandler onFile = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _decoder.Append(data);

            while (_decoder.TryReadFrame(out var kind, out var frame, out var fileContent, out var heartAt))
            {
                switch (kind)
                {
                    case FrameKind.Heart:
                        if (onHeart != null) onHeart(heartAt);
                        break;
                    case FrameKind.File:
                        if (onFile != null) onFile(fileContent);
                        break;
                    case FrameKind.Data:
                        handler.OnFrame(in frame);
                        break;
                }
            }
        }

        /// <summary>从帧头读取长度（8B 小端）。</summary>
        public static long GetLength(ReadOnlySpan<byte> data)
        {
            if (data.Length < P_LEN)
                throw new ArgumentException("数据长度不足");
            return BinaryPrimitives.ReadInt64LittleEndian(data);
        }

        public void Clear()
        {
            _decoder?.Clear();
        }
    }
}