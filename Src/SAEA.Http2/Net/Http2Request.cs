/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：Http2Request
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 14:23:19
*描述：
*=====================================================================
*修改时间：2019/6/28 14:23:19
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Http2.Net
{
    public class Http2Request : Http2Result, IDisposable
    {
        public string Method
        {
            get;
            private set;
        }

        public string Path
        {
            get;
            private set;
        }

        internal static Http2Request Parse(IStream stream)
        {
            Http2Request request = new Http2Request();
            try
            {
                request.Heads = stream.ReadHeadersAsync().GetAwaiter().GetResult().ToList();

                request.Method = request.Heads.First(h => h.Name == ":method").Value;

                request.Path = request.Heads.First(h => h.Name == ":path").Value;

                List<Byte> list = new List<byte>();
                while (true)
                {
                    var buf = new byte[2048];
                    var readResult = stream.ReadAsync(new ArraySegment<byte>(buf)).GetAwaiter().GetResult();
                    if (readResult.EndOfStream) break;
                    list.AddRange(buf.AsSpan().Slice(0, readResult.BytesRead).ToArray());
                }

                request.Body = list.ToArray();

                list.Clear();
            }
            catch (Exception ex)
            {
                request = null;
            }

            return request;
        }

        public void Dispose()
        {
            if (this.Heads != null && this.Heads.Any()) this.Heads.Clear();
            if (this.Body != null && this.Body.Any()) Array.Clear(this.Body, 0, this.Body.Length);
        }
    }

}
