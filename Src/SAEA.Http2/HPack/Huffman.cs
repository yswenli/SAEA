/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：Huffman
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:22:45
*描述：
*=====================================================================
*修改时间：2019/6/27 15:22:45
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;
using System.Buffers;
using System.Text;

namespace SAEA.Http2.HPack
{
    public class Huffman
    {
        /// <summary>
        /// 通过分配新缓冲区并将旧缓冲区中使用的字节复制到新缓冲区来调整缓冲区的大小。
        /// </summary>
        /// <param name="outBuf"></param>
        /// <param name="usedBytes"></param>
        /// <param name="newBytes"></param>
        /// <param name="pool"></param>
        private static void ResizeBuffer(
            ref byte[] outBuf, int usedBytes, int newBytes,
            ArrayPool<byte> pool)
        {
            var newSize = outBuf.Length + newBytes;
            var newBuf = pool.Rent(newSize);
            Array.Copy(outBuf, 0, newBuf, 0, usedBytes);
            pool.Return(outBuf);
            outBuf = newBuf;
        }

        public static string Decode(ArraySegment<byte> input, ArrayPool<byte> pool)
        {

            var byteCount = 0;

            var estLength = (input.Count * 3 + 1) / 2;

            var outBuf = pool.Rent(estLength);


            var inputByteOffset = 0;
            var inputBitOffset = 0;

            var currentSymbolLength = 0;

            var isValidPadding = true;

            var treeNode = HuffmanTree.Root;

            for (inputByteOffset = 0; inputByteOffset < input.Count; inputByteOffset++)
            {
                var bt = input.Array[inputByteOffset];
                for (inputBitOffset = 7; inputBitOffset >= 0; inputBitOffset--)
                {
                    var bit = (bt & (1 << inputBitOffset)) >> inputBitOffset;

                    if (bit != 0)
                    {
                        treeNode = treeNode.Child1;
                    }
                    else
                    {
                        treeNode = treeNode.Child0;
                        isValidPadding = false;
                    }
                    currentSymbolLength++;

                    if (treeNode == null)
                    {
                        pool.Return(outBuf);
                        throw new Exception("Invalid huffman code");
                    }

                    if (treeNode.Value != -1)
                    {
                        if (treeNode.Value != 256)
                        {

                            if (outBuf.Length - byteCount == 0)
                            {

                                var unprocessedBytes = input.Count - inputByteOffset;
                                ResizeBuffer(
                                    ref outBuf, byteCount, 2 * unprocessedBytes,
                                    pool);
                            }
                            outBuf[byteCount] = (byte)treeNode.Value;
                            byteCount++;
                            treeNode = HuffmanTree.Root;
                            currentSymbolLength = 0;
                            isValidPadding = true;
                        }
                        else
                        {
                            pool.Return(outBuf);
                            throw new Exception("Encountered EOS in huffman code");
                        }
                    }

                }
            }

            if (currentSymbolLength > 7)
            {
                throw new Exception("Padding exceeds 7 bits");
            }

            if (!isValidPadding)
            {
                throw new Exception("Invalid padding");
            }

            var str = Encoding.ASCII.GetString(outBuf, 0, byteCount);
            pool.Return(outBuf);
            return str;
        }

        /// <summary>
        /// 以哈夫曼编码形式返回字符串的长度
        /// </summary>
        public static int EncodedLength(ArraySegment<byte> data)
        {
            var byteCount = 0;
            var bitCount = 0;

            for (var i = data.Offset; i < data.Offset + data.Count; i++)
            {
                var tableEntry = HuffmanTable.Entries[data.Array[i]];
                bitCount += tableEntry.Len;
                if (bitCount >= 8)
                {
                    byteCount += bitCount / 8;
                    bitCount = bitCount % 8;
                }
            }

            if (bitCount != 0)
            {
                byteCount++;
            }

            return byteCount;
        }

        /// <summary>
        /// 对输入缓冲区执行Huffman编码。
        /// 输出必须足够大，以使哈夫曼编码字符串保持在指定的偏移量。
        /// 所需尺寸可使用Huffman.EncodedLength计算。
        /// </summary>
        /// <returns>
        /// 缓冲区中用于编码字符串的字节数。如果没有足够的可用空间进行编码，将返回-1
        /// </returns>
        public static int EncodeInto(
            ArraySegment<byte> buf,
            ArraySegment<byte> bytes)
        {
            if (bytes.Count == 0) return 0;

            var bitOffset = 0;
            var byteOffset = buf.Offset;
            var currentValue = 0;

            for (var i = bytes.Offset; i < bytes.Offset + bytes.Count; i++)
            {

                var tableEntry = HuffmanTable.Entries[bytes.Array[i]];
                var binVal = tableEntry.Bin;
                var bits = tableEntry.Len;

                while (bits > 0)
                {
                    var bitsToPut = 8 - bitOffset;
                    if (bits < bitsToPut) bitsToPut = bits;

                    var putBytes = binVal >> (bits - bitsToPut);

                    putBytes = putBytes << (8 - bitsToPut);
                    currentValue = currentValue | (putBytes >> bitOffset);
                    bitOffset += bitsToPut;
                    if (bitOffset == 8)
                    {
                        buf.Array[byteOffset] = (byte)(currentValue & 0xFF);
                        byteOffset++;
                        bitOffset = 0;
                        currentValue = 0;
                    }
                    bits -= bitsToPut;

                    binVal &= ((1 << bits) - 1);
                }
            }
            if (bitOffset != 0)
            {
                var eosBits = (1 << (8 - bitOffset)) - 1;
                currentValue |= eosBits;
                buf.Array[byteOffset] = (byte)(currentValue & 0xFF);
                byteOffset++;
            }

            return byteOffset - buf.Offset;
        }
    }
}
