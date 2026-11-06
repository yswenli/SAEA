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
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
        /// <summary>
        /// 帧体长度字段字节数（8 字节小端）
        /// </summary>
        public const int P_LEN = 8;
        /// <summary>
        /// 帧类型字段字节数（1 字节）
        /// </summary>
        public const int P_Type = 1;
        /// <summary>
        /// 帧头总字节数（长度 8 字节 + 类型 1 字节），帧体紧随其后
        /// </summary>
        public const int P_Head = 9;
        /// <summary>
        /// 小数据分界阈值（4KB，与内存池小缓冲区阈值一致），供发送与缓冲策略按包体大小分流
        /// </summary>
        public const int SmallDataThreshold = 4 * 1024;

        /// <summary>单帧最大帧体长度，可由部署方收紧。构造 FrameDecoder 时捕获。</summary>
        public static int MaxFrameLength { get; set; } = int.MaxValue - P_Head;

        private FrameDecoder _decoder = new FrameDecoder(MaxFrameLength);

        /// <summary>
        /// 把协议对象按线格式写入 writer：8 字节小端长度 + 1 字节类型 + 帧体
        /// </summary>
        /// <param name="protocal">待编码的协议对象</param>
        /// <param name="writer">目标缓冲写入器</param>
        public void Encode(ISocketProtocal protocal, IBufferWriter<byte> writer)
        {
            protocal.WriteTo(writer);
        }

        /// <summary>
        /// 有状态批量解码，输入可跨多段；返回批次须 using 释放以归还池化缓冲；非线程安全
        /// </summary>
        /// <param name="data">待解码的字节序列</param>
        /// <param name="onHeart">心跳帧回调，参数为心跳时间；为 null 则忽略心跳帧</param>
        /// <param name="onFile">文件帧回调，参数内容仅在批次释放前有效；为 null 则忽略文件帧</param>
        /// <returns>解码出的数据帧批次，须 using 释放</returns>
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
        /// 但省去调用方把 span 先物化为数组的中间拷贝；非线程安全。返回批次须 using。
        /// </summary>
        /// <param name="data">待解码的连续字节片段</param>
        /// <param name="onHeart">心跳帧回调，参数为心跳时间；为 null 则忽略心跳帧</param>
        /// <param name="onFile">文件帧回调，参数内容仅在批次释放前有效；为 null 则忽略文件帧</param>
        /// <returns>解码出的数据帧批次，须 using 释放</returns>
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

        /// <summary>
        /// 零拷贝流式解码：帧体仅在回调期间有效；与 Decode 共享解码状态，不得并发调用
        /// </summary>
        /// <param name="data">待解码的字节片段，可多次追加，未成帧的部分留待下次</param>
        /// <param name="handler">数据帧处理器，不能为 null，帧体仅在 OnFrame 回调期间有效</param>
        /// <param name="onHeart">心跳帧回调，参数为心跳时间；为 null 则忽略心跳帧</param>
        /// <param name="onFile">文件帧回调，参数内容仅在回调期间有效；为 null 则忽略文件帧</param>
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

        /// <summary>
        /// 从帧头读取帧体长度（8 字节小端）
        /// </summary>
        /// <param name="data">至少包含 8 字节长度字段的数据</param>
        /// <returns>帧体字节数</returns>
        public static long GetLength(ReadOnlySpan<byte> data)
        {
            if (data.Length < P_LEN)
                throw new ArgumentException("数据长度不足");
            return BinaryPrimitives.ReadInt64LittleEndian(data);
        }

        /// <summary>
        /// 清空解码器状态并归还内部累加缓冲，丢弃未成帧的残留数据；连接复用时须调用
        /// </summary>
        public void Clear()
        {
            _decoder?.Clear();
        }
    }
}