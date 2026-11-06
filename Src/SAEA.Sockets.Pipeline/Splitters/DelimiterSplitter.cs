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
*文件名： DelimiterSplitter
*版本号： v26.11.5.1
*唯一标识：dfe8e8a0-4f37-4344-9915-9778b273e65e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：基于分隔符的分帧器：在连续缓冲中查找首个完整分隔符，再按配置决定是否把分隔符留在帧内
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：基于分隔符的分帧器：在连续缓冲中查找首个完整分隔符，再按配置决定是否把分隔符留在帧内
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 基于分隔符的分帧器：在连续缓冲中查找首个完整分隔符，再按配置决定是否把分隔符留在帧内。
    /// 无状态、非线程安全，同一实例可按会话复用。
    /// </summary>
    public sealed class DelimiterSplitter : IFrameSplitter
    {
        private readonly byte[] _delimiter;
        private readonly int _maxFrameLength;
        private readonly bool _stripDelimiter;

        /// <summary>可接受的最大帧长，按整帧长度（含分隔符）解释。</summary>
        public int MaxFrameLength
        {
            get { return _maxFrameLength; }
        }

        /// <summary>
        /// 创建分帧器。
        /// </summary>
        /// <param name="delimiter">帧分隔符，不能为空。</param>
        /// <param name="maxFrameLength">整帧长度上限，必须大于 0。</param>
        /// <param name="stripDelimiter">产出帧时是否剥掉分隔符。</param>
        public DelimiterSplitter(byte[] delimiter, int maxFrameLength, bool stripDelimiter = true)
        {
            if (delimiter == null || delimiter.Length == 0)
                throw new ArgumentException("分隔符不能为 null 或空。", nameof(delimiter));
            if (maxFrameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength 必须大于 0。");

            _delimiter = new byte[delimiter.Length];
            Buffer.BlockCopy(delimiter, 0, _delimiter, 0, delimiter.Length);
            _maxFrameLength = maxFrameLength;
            _stripDelimiter = stripDelimiter;
        }

        /// <summary>
        /// 以单字节分隔符创建分帧器。
        /// </summary>
        /// <param name="delimiter">帧分隔符。</param>
        /// <param name="maxFrameLength">整帧长度上限，必须大于 0。</param>
        /// <param name="stripDelimiter">产出帧时是否剥掉分隔符。</param>
        public DelimiterSplitter(byte delimiter, int maxFrameLength, bool stripDelimiter = true)
            : this(new[] { delimiter }, maxFrameLength, stripDelimiter)
        {
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlyMemory<byte> buffered, out int consumed, out ReadOnlyMemory<byte> frame)
        {
            consumed = 0;
            frame = ReadOnlyMemory<byte>.Empty;

            var index = IndexOfDelimiter(buffered.Span);
            if (index < 0)
            {
                if (buffered.Length > _maxFrameLength)
                    throw new FrameProtocolException(
                        $"未找到分隔符且缓冲长度 {buffered.Length} 超过上限 {_maxFrameLength}。");
                return false;
            }

            consumed = index + _delimiter.Length;
            frame = _stripDelimiter
                ? buffered.Slice(0, index)
                : buffered.Slice(0, consumed);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }

        private int IndexOfDelimiter(ReadOnlySpan<byte> span)
        {
            var last = span.Length - _delimiter.Length;
            for (var i = 0; i <= last; i++)
            {
                if (span[i] != _delimiter[0]) continue;

                var matched = true;
                for (var j = 1; j < _delimiter.Length; j++)
                {
                    if (span[i + j] != _delimiter[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched) return i;
            }

            return -1;
        }
    }
}