/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：IntDecoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 14:21:13
*描述：
*=====================================================================
*修改时间：2019/6/27 14:21:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.HPack
{
    /// <summary>
    /// 根据hpack规范解码整数值。
    /// </summary>
    public class IntDecoder
    {

        public int Result;

        public bool Done = true;

        private int _acc = 0;

        /// <summary>
        ///从输入缓冲区开始对整数解码
        ///带有给定前缀。输入缓冲区必须至少有一个
        ///在给定的偏移量下可用的单可读字节。如果在此调用期间可以解码一个完整的整数，则结果成员将设置为结果，
        ///完成成员将设置为true。否则，需要更多的数据，在读取结果之前，必须使用新的缓冲区数据调用decodecont，
        ///直到done设置为true。
        /// </summary>
        public int Decode(int prefixLen, ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var length = buf.Count;

            var bt = buf.Array[offset];
            offset++;
            length--;
            var consumed = 1;

            var prefixMask = (1 << (prefixLen)) - 1;
            this.Result = bt & prefixMask;
            if (prefixMask == this.Result)
            {
                this._acc = 0;
                this.Done = false;
                consumed += this.DecodeCont(new ArraySegment<byte>(buf.Array, offset, length));
            }
            else
            {
                this.Done = true;
            }

            return consumed;
        }

        /// <summary>
        /// 继续使用新的输入缓冲区数据解码整数。
        /// </summary>
        public int DecodeCont(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var length = buf.Count;

            while (length > 0)
            {
                var bt = buf.Array[offset];
                offset++;
                length--;

                var add = (bt & 127) * (1u << _acc);
                var n = add + this.Result;
                if (n > Int32.MaxValue)
                {
                    throw new Exception("invalid integer");
                }

                this.Result = (int)n;
                this._acc += 7;

                if ((bt & 0x80) == 0)
                {
                    this.Done = true;
                    return offset - buf.Offset;
                }
            }

            return offset - buf.Offset;
        }
    }
}
