/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：UpgradeReadStream
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 16:33:41
*描述：
*=====================================================================
*修改时间：2019/6/28 16:33:41
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Http2.Core
{
    class UpgradeReadStream : IReadableByteStream
    {
        IReadableByteStream stream;
        byte[] httpBuffer = new byte[MaxHeaderLength];
        int httpBufferOffset = 0;
        int httpHeaderLength = 0;

        ArraySegment<byte> remains;

        const int MaxHeaderLength = 1024;

        public int HttpHeaderLength => httpHeaderLength;

        public ArraySegment<byte> HeaderBytes =>
            new ArraySegment<byte>(httpBuffer, 0, HttpHeaderLength);

        public UpgradeReadStream(IReadableByteStream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        /// 等待直到收到由\r\n\r\n终止的整个HTTP/1头。
        /// </summary>
        /// <returns></returns>
        public async Task WaitForHttpHeader()
        {
            while (true)
            {
                var res = await stream.ReadAsync(
                    new ArraySegment<byte>(httpBuffer, httpBufferOffset, httpBuffer.Length - httpBufferOffset));

                if (res.EndOfStream)
                    throw new System.IO.EndOfStreamException();
                httpBufferOffset += res.BytesRead;

                var str = Encoding.ASCII.GetString(httpBuffer, 0, httpBufferOffset);
                var endOfHeaderIndex = str.IndexOf("\r\n\r\n");
                if (endOfHeaderIndex == -1)
                {
                    if (httpBufferOffset == httpBuffer.Length)
                    {
                        httpBuffer = null;
                        throw new Exception("未接收到HTTP头");
                    }
                }
                else
                {
                    httpHeaderLength = endOfHeaderIndex + 4;
                    return;
                }
            }
        }

        /// <summary>
        /// 将HTTP读卡器标记为未读，这意味着以下readasync调用将重新读取头。
        /// </summary>
        public void UnreadHttpHeader()
        {
            remains = new ArraySegment<byte>(
                httpBuffer, 0, httpBufferOffset);
        }

        /// <summary>
        /// 从输入缓冲区中删除接收到的HTTP头
        /// </summary>
        public void ConsumeHttpHeader()
        {
            if (httpHeaderLength != httpBufferOffset)
            {
                remains = new ArraySegment<byte>(
                    httpBuffer, httpHeaderLength, httpBufferOffset - httpHeaderLength);
            }
            else
            {
                remains = new ArraySegment<byte>();
                httpBuffer = null;
            }
        }

        /// <summary>
        /// 从缓冲区读取
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public ValueTask<StreamReadResult> ReadAsync(ArraySegment<byte> buffer)
        {
            if (remains.Count != 0)
            {
                var toCopy = Math.Min(remains.Count, buffer.Count);
                Array.Copy(
                    remains.Array, remains.Offset,
                    buffer.Array, buffer.Offset,
                    toCopy);
                var newOffset = remains.Offset + toCopy;
                var newCount = remains.Count - toCopy;
                if (newCount != 0)
                {
                    remains = new ArraySegment<byte>(remains.Array, newOffset, newCount);
                }
                else
                {
                    remains = new ArraySegment<byte>();
                    httpBuffer = null;
                }

                return new ValueTask<StreamReadResult>(
                    new StreamReadResult()
                    {
                        BytesRead = toCopy,
                        EndOfStream = false,
                    });
            }

            return stream.ReadAsync(buffer);
        }
    }
}
