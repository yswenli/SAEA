/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：PriorityData
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 17:14:18
*描述：
*=====================================================================
*修改时间：2019/6/27 17:14:18
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 多帧共享的优先级数据
    /// </summary>
    public struct PriorityData
    {
        public uint StreamDependency;

        public bool StreamDependencyIsExclusive;

        public byte Weight;

        public const int Size = 5;

        public void EncodeInto(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            b[o + 0] = (byte)((StreamDependency >> 24) & 0x7F);
            b[o + 1] = (byte)((StreamDependency >> 16) & 0xFF);
            b[o + 2] = (byte)((StreamDependency >> 8) & 0xFF);
            b[o + 3] = (byte)((StreamDependency) & 0xFF);
            if (StreamDependencyIsExclusive) b[o + 0] |= 0x80;
            b[o + 4] = Weight;
        }

        public static PriorityData DecodeFrom(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            var dep = (((uint)b[o + 0] & 0x7F) << 24)
                | ((uint)b[o + 1] << 16)
                | (uint)(b[o + 2] << 8)
                | (uint)b[o + 3];
            var exclusive = (b[o + 0] & 0x80) != 0;
            var weight = b[o + 4];

            return new PriorityData
            {
                StreamDependency = dep,
                StreamDependencyIsExclusive = exclusive,
                Weight = weight,
            };
        }
    }
}
