/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：StringEncoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:43:47
*描述：
*=====================================================================
*修改时间：2019/6/27 15:43:47
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;
using System.Text;

namespace SAEA.Http2.HPack
{
    /// <summary>
    /// 根据encodes to the hpack字符串值的名称。
    /// </summary>
    public static class StringEncoder
    {
        /// <summary>
        /// 以非哈夫曼编码形式返回给定字符串的字节长度。
        /// </summary>
        public static int GetByteLength(string value)
        {
            return Encoding.ASCII.GetByteCount(value);
        }

        /// <summary>
        /// 将给定的字符串编码到目标缓冲区中
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="value"></param>
        /// <param name="valueByteLen"></param>
        /// <param name="huffman"></param>
        /// <returns></returns>
        public static int EncodeInto(
            ArraySegment<byte> buf,
            string value, int valueByteLen, HuffmanStrategy huffman)
        {
            var offset = buf.Offset;
            var free = buf.Count;

            if (free < 1 + valueByteLen) return -1;

            var encodedByteLen = valueByteLen;
            var requiredHuffmanBytes = 0;
            var useHuffman = huffman == HuffmanStrategy.Always;
            byte[] huffmanInputBuf = null;


            if (huffman == HuffmanStrategy.Always || huffman == HuffmanStrategy.IfSmaller)
            {
                huffmanInputBuf = Encoding.ASCII.GetBytes(value);
                requiredHuffmanBytes = Huffman.EncodedLength(
                    new ArraySegment<byte>(huffmanInputBuf));
                if (huffman == HuffmanStrategy.IfSmaller && requiredHuffmanBytes < encodedByteLen)
                {
                    useHuffman = true;
                }
            }

            if (useHuffman)
            {
                encodedByteLen = requiredHuffmanBytes;
            }


            var prefixContent = useHuffman ? (byte)0x80 : (byte)0;
            var used = IntEncoder.EncodeInto(
                new ArraySegment<byte>(buf.Array, offset, free),
                encodedByteLen, prefixContent, 7);
            if (used == -1) return -1; 
            offset += used;
            free -= used;

            if (useHuffman)
            {
                if (free < requiredHuffmanBytes) return -1;

                used = Huffman.EncodeInto(
                    new ArraySegment<byte>(buf.Array, offset, free),
                    new ArraySegment<byte>(huffmanInputBuf));
                if (used == -1) return -1; 
                offset += used;
            }
            else
            {
                if (free < valueByteLen) return -1;

                used = Encoding.ASCII.GetBytes(
                    value, 0, value.Length, buf.Array, offset);
                offset += used;
            }

            return offset - buf.Offset;
        }

        /// <summary>
        /// 对给定的字符串进行编码。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="huffman"></param>
        /// <returns></returns>
        public static byte[] Encode(string value, HuffmanStrategy huffman)
        {
            var asciiSize = Encoding.ASCII.GetByteCount(value);
            var estimatedHeaderLength = IntEncoder.RequiredBytes(asciiSize, 0, 7);
            var estimatedBufferSize = estimatedHeaderLength + asciiSize;

            while (true)
            {
                var buf = new byte[estimatedBufferSize + 16];

                var size = EncodeInto(
                    new ArraySegment<byte>(buf), value, asciiSize, huffman);
                if (size != -1)
                {
                    if (size == buf.Length) return buf;
                    var newBuf = new byte[size];
                    Array.Copy(buf, 0, newBuf, 0, size);
                    return newBuf;
                }
                else
                {
                    estimatedBufferSize = (estimatedBufferSize + 2) * 2;
                }
            }
        }
    }
}
