/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：IntEncoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:41:26
*描述：
*=====================================================================
*修改时间：2019/6/27 15:41:26
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.HPack
{
    /// <summary>
    /// 根据hpack规范对整数值进行编码。
    /// </summary>
    public static class IntEncoder
    {
        public static int RequiredBytes(int value, byte beforePrefix, int prefixBits)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            var offset = 0;

            int maxPrefixVal = ((1 << prefixBits) - 1); // 2^N - 1
            if (value < maxPrefixVal)
            {
                offset++;
            }
            else
            {
                offset++;
                value -= maxPrefixVal;
                while (value >= 128)
                {
                    offset++;
                    value = value / 128; // 32bit
                }
                offset++;
            }

            return offset;
        }

        /// <summary>
        /// 将给定的数字编码到目标缓冲区中
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="value"></param>
        /// <param name="beforePrefix"></param>
        /// <param name="prefixBits"></param>
        /// <returns></returns>
        public static int EncodeInto(
            ArraySegment<byte> buf, int value, byte beforePrefix, int prefixBits)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            var offset = buf.Offset;
            int free = buf.Count;
            if (free < 1) return -1;

            int maxPrefixVal = ((1 << prefixBits) - 1); // 2^N - 1
            if (value < maxPrefixVal)
            {
                buf.Array[offset] = (byte)((beforePrefix | value) & 0xFF);
                offset++;
            }
            else
            {
                buf.Array[offset] = (byte)((beforePrefix | maxPrefixVal) & 0xFF);
                offset++;
                free--;
                if (free < 1) return -1;
                value -= maxPrefixVal;
                while (value >= 128)
                {
                    var part = (value % 128 + 128);
                    buf.Array[offset] = (byte)(part & 0xFF);
                    offset++;
                    free--;
                    if (free < 1) return -1;
                    value = value / 128; // 32bit
                }
                buf.Array[offset] = (byte)(value & 0xFF);
                offset++;
            }

            return offset - buf.Offset;
        }

        /// <summary>
        /// 对给定的数字进行编码
        /// </summary>
        /// <param name="value"></param>
        /// <param name="beforePrefix"></param>
        /// <param name="prefixBits"></param>
        /// <returns></returns>
        public static byte[] Encode(int value, byte beforePrefix, int prefixBits)
        {
            var bytes = new byte[RequiredBytes(value, beforePrefix, prefixBits)];
            EncodeInto(new ArraySegment<byte>(bytes), value, beforePrefix, prefixBits);
            return bytes;
        }
    }
}
