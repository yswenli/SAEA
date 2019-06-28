/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：GoAwayFrameData
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:41:41
*描述：
*=====================================================================
*修改时间：2019/6/27 16:41:41
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 在goaway帧中携带的数据
    /// </summary>
    public struct GoAwayFrameData
    {
        public GoAwayReason Reason;

        public int RequiredSize => 8 + Reason.DebugData.Count;

        public void EncodeInto(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;
            var errc = (uint)Reason.ErrorCode;

            b[o + 0] = (byte)((Reason.LastStreamId >> 24) & 0xFF);
            b[o + 1] = (byte)((Reason.LastStreamId >> 16) & 0xFF);
            b[o + 2] = (byte)((Reason.LastStreamId >> 8) & 0xFF);
            b[o + 3] = (byte)((Reason.LastStreamId) & 0xFF);
            b[o + 4] = (byte)((errc >> 24) & 0xFF);
            b[o + 5] = (byte)((errc >> 16) & 0xFF);
            b[o + 6] = (byte)((errc >> 8) & 0xFF);
            b[o + 7] = (byte)((errc) & 0xFF);
            Array.Copy(
                Reason.DebugData.Array, Reason.DebugData.Offset,
                b, o + 8,
                Reason.DebugData.Count);
        }

        public static GoAwayFrameData DecodeFrom(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            var lastStreamId =
                (((uint)b[o + 0] & 0x7F) << 24)
                | ((uint)b[o + 1] << 16)
                | ((uint)b[o + 2] << 8)
                | (uint)b[o + 3];
            var errc =
                ((uint)b[o + 4] << 24)
                | ((uint)b[o + 5] << 16)
                | ((uint)b[o + 6] << 8)
                | (uint)b[o + 7];
            var debugData = new ArraySegment<byte>(b, o + 8, bytes.Count - 8);

            return new GoAwayFrameData
            {
                Reason = new GoAwayReason
                {
                    LastStreamId = lastStreamId,
                    ErrorCode = (ErrorCode)errc,
                    DebugData = debugData,
                },
            };
        }
    }

    /// <summary>
    /// 描述发送Goaway的原因
    /// </summary>
    public struct GoAwayReason
    {
        public uint LastStreamId;
        public ErrorCode ErrorCode;
        public ArraySegment<byte> DebugData;
    }
}
