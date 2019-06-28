/****************************************************************************
*项目名称：SAEA.Http2.Extentions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Extentions
*类 名 称：ByteStreamExtensions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:33:59
*描述：
*=====================================================================
*修改时间：2019/6/27 16:33:59
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Threading.Tasks;

namespace SAEA.Http2.Extentions
{
    /// <summary>
    /// 用于处理字节流的实用程序和扩展函数
    /// </summary>
    public static class ByteStreamExtensions
    {
        public async static ValueTask<DoneHandle> ReadAll(
           this IReadableByteStream stream, ArraySegment<byte> buffer)
        {
            var array = buffer.Array;
            var offset = buffer.Offset;
            var count = buffer.Count;


            while (count != 0)
            {
                var segment = new ArraySegment<byte>(array, offset, count);
                var res = await stream.ReadAsync(segment);
                if (res.EndOfStream)
                {
                    throw new System.IO.EndOfStreamException();
                }
                offset += res.BytesRead;
                count -= res.BytesRead;
            }

            return DoneHandle.Instance;
        }
    }
}
