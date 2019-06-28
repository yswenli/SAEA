/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：FrameHeader
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:31:14
*描述：
*=====================================================================
*修改时间：2019/6/27 16:31:14
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Extentions;
using SAEA.Http2.Interfaces;
using System;
using System.Threading.Tasks;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// A header of an http/2 frame
    /// </summary>
    public struct FrameHeader
    {
        public FrameType Type;

        public uint StreamId;

        public int Length;

        public byte Flags;

        public const int HeaderSize = 9;

        public static FrameHeader DecodeFrom(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;
            var length = (b[o + 0] << 16) | (b[o + 1] << 8) | (b[o + 2] & 0xFF);
            var type = (FrameType)b[o + 3];
            var flags = b[o + 4];
            var streamId = ((b[o + 5] & 0x7F) << 24) | (b[o + 6] << 16) | (b[o + 7] << 8) | b[o + 8];
            return new FrameHeader
            {
                Type = type,
                Length = length,
                Flags = flags,
                StreamId = (uint)streamId,
            };
        }

        public void EncodeInto(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            var length = (b[o + 0] << 16) | (b[o + 1] << 8) | (b[o + 2] & 0xFF);
            b[o + 0] = (byte)((Length >> 16) & 0xFF);
            b[o + 1] = (byte)((Length >> 8) & 0xFF);
            b[o + 2] = (byte)((Length) & 0xFF);
            b[o + 3] = (byte)Type;
            b[o + 4] = Flags;
            b[o + 5] = (byte)((StreamId >> 24) & 0xFF);
            b[o + 6] = (byte)((StreamId >> 16) & 0xFF);
            b[o + 7] = (byte)((StreamId >> 8) & 0xFF);
            b[o + 8] = (byte)((StreamId) & 0xFF);
        }

        public bool HasEndOfStreamFlag
        {
            get
            {
                switch (Type)
                {
                    case FrameType.Data:
                        return (Flags & (byte)DataFrameFlags.EndOfStream) != 0;
                    case FrameType.Headers:
                        return (Flags & (byte)HeadersFrameFlags.EndOfStream) != 0;
                    default:
                        return false;
                }
            }
        }


        public static async ValueTask<FrameHeader> ReceiveAsync(
            IReadableByteStream stream, byte[] headerSpace)
        {
            await stream.ReadAll(new ArraySegment<byte>(headerSpace, 0, HeaderSize));
            return DecodeFrom(new ArraySegment<byte>(headerSpace, 0, HeaderSize));
        }
    }
}
