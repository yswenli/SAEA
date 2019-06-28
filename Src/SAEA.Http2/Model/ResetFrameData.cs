/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：ResetFrameData
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:40:59
*描述：
*=====================================================================
*修改时间：2019/6/27 16:40:59
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 在复位帧内携带的数据
    /// </summary>
    public struct ResetFrameData
    {
        public ErrorCode ErrorCode;

        public const int Size = 4;

        public void EncodeInto(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;
            var ec = (uint)ErrorCode;

            b[o + 0] = (byte)((ec >> 24) & 0xFF);
            b[o + 1] = (byte)((ec >> 16) & 0xFF);
            b[o + 2] = (byte)((ec >> 8) & 0xFF);
            b[o + 3] = (byte)((ec) & 0xFF);
        }

        public static ResetFrameData DecodeFrom(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            var errc =
                ((uint)b[o + 0] << 24)
                | ((uint)b[o + 1] << 16)
                | ((uint)b[o + 2] << 8)
                | (uint)b[o + 3];

            return new ResetFrameData
            {
                ErrorCode = (ErrorCode)errc,
            };
        }
    }
}
