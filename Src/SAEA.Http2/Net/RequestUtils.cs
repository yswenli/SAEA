/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：RequestUtils
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 13:56:35
*描述：
*=====================================================================
*修改时间：2019/6/28 13:56:35
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using System;
using System.Threading.Tasks;

namespace SAEA.Http2.Net
{
    /// <summary>
    /// 请求工具类
    /// </summary>
    public static class RequestUtils
    {
        /// <summary>
        /// 清空流
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public async static Task DrainAsync(this IReadableByteStream stream)
        {
            var buf = Buffers.Pool.Rent(8 * 1024);

            var bytesRead = 0;

            try
            {
                while (true)
                {
                    var res = await stream.ReadAsync(new ArraySegment<byte>(buf));
                    if (res.BytesRead != 0)
                    {
                        bytesRead += res.BytesRead;
                    }

                    if (res.EndOfStream)
                    {
                        return;
                    }
                }
            }
            finally
            {
                Buffers.Pool.Return(buf);
            }
        }

        /// <summary>
        /// 转发流
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        public async static Task CopyToAsync(
            this IReadableByteStream stream,
            IWriteableByteStream dest)
        {
            var buf = Buffers.Pool.Rent(64 * 1024);

            var bytesRead = 0;

            try
            {
                while (true)
                {
                    var res = await stream.ReadAsync(new ArraySegment<byte>(buf));
                    if (res.BytesRead != 0)
                    {
                        await dest.WriteAsync(new ArraySegment<byte>(buf, 0, res.BytesRead));
                        bytesRead += res.BytesRead;
                    }

                    if (res.EndOfStream)
                    {
                        return;
                    }
                }
            }
            finally
            {
                Buffers.Pool.Return(buf);
            }
        }
    }
}
