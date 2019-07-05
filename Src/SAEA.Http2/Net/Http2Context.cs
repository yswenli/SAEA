/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：Http2Context
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 14:34:13
*描述：
*=====================================================================
*修改时间：2019/6/28 14:34:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using System;

namespace SAEA.Http2.Net
{
    public class Http2Context : IDisposable
    {
        IHttp2Invoker _invoker;

        public Http2Request Request
        {
            get;
            private set;
        }

        public Http2Response Response
        {
            get;
            private set;
        }

        internal Http2Context(IStream stream, IHttp2Invoker invoker = null)
        {
            Request = Http2Request.Parse(stream);
            Response = new Http2Response(stream);

            if (_invoker == null)
            {
                _invoker = new WebInvoker("wwwroot");
            }
        }

        public void Invoke()
        {
            _invoker.Invoke(this);
        }

        public void Dispose()
        {
            Request.Dispose();
            Response.Dispose();
        }
    }
}
