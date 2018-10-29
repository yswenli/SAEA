/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http
*文件名： HttpContext
*版本号： V3.1.1.0
*唯一标识：af0b65c6-0f58-4221-9e52-7e3f0a4ffb24
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:46:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:46:31
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA.Http http上下文
    /// </summary>
    public class HttpContext 
    {
        public HttpRequest Request
        {
            get;
            private set;
        }

        public HttpResponse Response
        {
            get;
            private set;
        }

        public HttpUtility Server
        {
            get;
            private set;
        }

        internal IInvoker Invoker { get; private set; }

        public WebConfig WebConfig { get; set; }

        internal HttpContext(IWebHost webHost, IUserToken userToken, RequestDataReader requestDataReader)
        {
            this.WebConfig = webHost.WebConfig;

            this.Invoker = webHost.Invoker;

            this.Request = new HttpRequest();

            this.Response = new HttpResponse();

            this.Request.Init(requestDataReader);

            this.Response.Init(webHost, userToken, this.Request.Protocal, webHost.WebConfig.IsZiped);

            this.Server = new HttpUtility(webHost.WebConfig.Root);

            IsStaticsCached = webHost.WebConfig.IsStaticsCached;
        }

        public bool IsStaticsCached { get; set; }

        public void HttpHandle()
        {
            Invoker.Invoke(this);           
        }
    }
}
