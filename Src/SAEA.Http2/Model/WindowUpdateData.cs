/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：WindowUpdateData
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:39:54
*描述：
*=====================================================================
*修改时间：2019/6/27 16:39:54
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 在窗口更新帧中携带的数据
    /// </summary>
    public struct WindowUpdateData
    {
        public int WindowSizeIncrement;

        public const int Size = 4;

        /// <summary>
        /// 将窗口更新数据编码到给定的字节数组中
        /// </summary>
        public void EncodeInto(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            b[o + 0] = (byte)((WindowSizeIncrement >> 24) & 0xFF);
            b[o + 1] = (byte)((WindowSizeIncrement >> 16) & 0xFF);
            b[o + 2] = (byte)((WindowSizeIncrement >> 8) & 0xFF);
            b[o + 3] = (byte)((WindowSizeIncrement) & 0xFF);
        }

        /// <summary>
        /// 将窗口更新数据编码到给定的字节数组中
        /// </summary>
        public static WindowUpdateData DecodeFrom(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            var increment =
                ((b[o + 0] & 0x7F) << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

            return new WindowUpdateData
            {
                WindowSizeIncrement = increment,
            };
        }
    }
}
