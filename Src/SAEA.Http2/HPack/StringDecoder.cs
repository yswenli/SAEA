/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：StringDecoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:04:10
*描述：
*=====================================================================
*修改时间：2019/6/27 15:04:10
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Buffers;
using System.Text;

namespace SAEA.Http2.HPack
{
    /// <summary>
    /// 根据decodes to the hpack字符串值的名称。
    /// </summary>
    public class StringDecoder : IDisposable
    {
        private enum State : byte
        {
            StartDecode,
            DecodeLength,
            DecodeData,
        }


        public string Result;

        public int StringLength;
        /// <summary>
        /// 解码是否完成。
        /// 这是在调用decode（）后设置的。
        /// 如果一个完整的整数可以从输入缓冲区解码
        /// 值为真。如果无法解码完整整数然后需要更多的字节，
        /// 必须调用decodecont，直到在读取结果之前完成为真。
        /// </summary>
        public bool Done = true;


        private bool _huffman;

        private State _state = State.StartDecode;

        private int _octetLength;


        private byte[] _stringBuffer;


        private int _bufferOffset;


        private int _maxLength;


        private IntDecoder _lengthDecoder = new IntDecoder();


        private ArrayPool<byte> _bufferPool;

        public StringDecoder(
            int maxLength, ArrayPool<byte> bufferPool)
        {
            if (maxLength < 1) throw new ArgumentException(nameof(maxLength));
            if (bufferPool == null) throw new ArgumentException(nameof(bufferPool));
            _maxLength = maxLength;
            _bufferPool = bufferPool;
        }

        public void Dispose()
        {
            if (_stringBuffer != null)
            {
                _bufferPool.Return(_stringBuffer);
                _stringBuffer = null;
            }

            _bufferPool = null;
        }

        public int Decode(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var length = buf.Count;

            if (_stringBuffer != null)
            {
                _bufferPool.Return(_stringBuffer);
                _stringBuffer = null;
            }

            var bt = buf.Array[offset];
            this._huffman = (bt & 0x80) == 0x80;
            this.Done = false;
            this._state = State.DecodeLength;
            var consumed = this._lengthDecoder.Decode(7, buf);
            length -= consumed;
            offset += consumed;

            if (this._lengthDecoder.Done)
            {
                var len = this._lengthDecoder.Result;
                if (len > this._maxLength)
                    throw new Exception("Maximum string length exceeded");
                this._octetLength = len;
                this._stringBuffer = _bufferPool.Rent(this._octetLength);
                this._bufferOffset = 0;
                this._state = State.DecodeData;
                consumed += this.DecodeCont(new ArraySegment<byte>(buf.Array, offset, length));
                return consumed;
            }
            else
            {
                return consumed;
            }
        }

        private int DecodeContLength(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var length = buf.Count;

            var consumed = this._lengthDecoder.DecodeCont(buf);
            length -= consumed;
            offset += consumed;

            if (this._lengthDecoder.Done)
            {
                var len = this._lengthDecoder.Result;
                if (len > this._maxLength)
                    throw new Exception("Maximum string length exceeded");
                this._octetLength = len;
                this._stringBuffer = _bufferPool.Rent(this._octetLength);
                this._bufferOffset = 0;
                this._state = State.DecodeData;
            }

            return consumed;
        }

        private int DecodeContByteData(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var count = buf.Count;

            var available = count;
            var need = this._octetLength - this._bufferOffset;

            var toCopy = available >= need ? need : available;
            if (toCopy > 0)
            {
                Array.Copy(buf.Array, offset, this._stringBuffer, this._bufferOffset, toCopy);
                this._bufferOffset += toCopy;

                offset += toCopy;
                count -= toCopy;
            }

            if (this._bufferOffset == this._octetLength)
            {

                var view = new ArraySegment<byte>(
                    this._stringBuffer, 0, this._octetLength
                );
                if (this._huffman)
                {
                    this.Result = Huffman.Decode(view, _bufferPool);
                }
                else
                {
                    this.Result =
                        Encoding.ASCII.GetString(view.Array, view.Offset, view.Count);
                }
                this.Done = true;

                this.StringLength = this.Result.Length;
                this._state = State.StartDecode;

                _bufferPool.Return(this._stringBuffer);
                this._stringBuffer = null;
            }

            return offset - buf.Offset;
        }

        public int DecodeCont(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var count = buf.Count;

            if (this._state == State.DecodeLength && count > 0)
            {
                var consumed = this.DecodeContLength(buf);
                offset += consumed;
                count -= consumed;
            }

            if (this._state == State.DecodeData)
            {
                var consumed = this.DecodeContByteData(
                    new ArraySegment<byte>(buf.Array, offset, count));
                offset += consumed;
                count -= consumed;
            }

            return offset - buf.Offset;
        }
    }
}
