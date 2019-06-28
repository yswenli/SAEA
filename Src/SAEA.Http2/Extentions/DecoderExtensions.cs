/****************************************************************************
*项目名称：SAEA.Http2.Extentions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Extentions
*类 名 称：DecoderExtensions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 10:42:59
*描述：
*=====================================================================
*修改时间：2019/6/28 10:42:59
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;

namespace SAEA.Http2.Extentions
{
    /// <summary>
    /// hpack解码器的扩展方法
    /// </summary>
    public static class DecoderExtensions
    {
        /// <summary>
        /// DecodeHeaderBlockFragment操作的状态
        /// </summary>
        public enum DecodeStatus
        {
            Success = 0,
            MaxHeaderListSizeExceeded = 1,
            IncompleteHeaderBlockFragment = 2,
            InvalidHeaderBlockFragment = 3,
        }

        /// <summary>
        /// DecodeHeaderBlockFragment操作的结果
        /// </summary>
        public struct DecodeFragmentResult
        {
            public DecodeStatus Status;

            public uint HeaderFieldsSize;
        }

        /// <summary>
        /// 使用给定的解码器解码整个头块片段。
        /// </summary>
        /// <param name="decoder"></param>
        /// <param name="buffer"></param>
        /// <param name="maxHeaderFieldsSize"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static DecodeFragmentResult DecodeHeaderBlockFragment(
            this HPack.Decoder decoder,
            ArraySegment<byte> buffer,
            uint maxHeaderFieldsSize,
            List<HeaderField> headers)
        {
            int offset = buffer.Offset;
            int length = buffer.Count;
            uint headersSize = 0;

            try
            {
                while (length > 0)
                {
                    var segment = new ArraySegment<byte>(buffer.Array, offset, length);
                    var consumed = decoder.Decode(segment);
                    offset += consumed;
                    length -= consumed;
                    if (decoder.Done)
                    {
                        headersSize += (uint)decoder.HeaderSize;
                        if (headersSize > maxHeaderFieldsSize)
                        {
                            headersSize -= (uint)decoder.HeaderSize;
                            return new DecodeFragmentResult
                            {
                                Status = DecodeStatus.MaxHeaderListSizeExceeded,
                                HeaderFieldsSize = headersSize,
                            };
                        }
                        headers.Add(decoder.HeaderField);
                    }
                }
            }
            catch (Exception)
            {
                return new DecodeFragmentResult
                {
                    Status = DecodeStatus.InvalidHeaderBlockFragment,
                    HeaderFieldsSize = headersSize,
                };
            }

            return new DecodeFragmentResult
            {
                Status = DecodeStatus.Success,
                HeaderFieldsSize = headersSize,
            };
        }
    }
}
