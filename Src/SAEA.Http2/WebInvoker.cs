/****************************************************************************
*项目名称：SAEA.Http2
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2
*类 名 称：WebInvoker
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 15:54:20
*描述：
*=====================================================================
*修改时间：2019/6/28 15:54:20
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using SAEA.Http2.Net;

namespace SAEA.Http2
{
    public class WebInvoker : IHttp2Invoker
    {
        public void Invoke(Http2Context http2Context)
        {
            var path = http2Context.Request.Path;


        }
    }
}
