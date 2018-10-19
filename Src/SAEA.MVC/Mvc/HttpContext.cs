/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Http
*文件名： HttpContext
*版本号： V2.2.1.1
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
*版本号： V2.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Base;
using SAEA.MVC.Model;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.MVC.Mvc
{
    /// <summary>
    /// SAEA.MVC http上下文
    /// </summary>
    public class HttpContext : IDisposable
    {
        public RequestBase Request
        {
            get;
            private set;
        }

        public ResponseBase Response
        {
            get;
            private set;
        }

        public HttpUtility Server
        {
            get;
            private set;
        }

        internal IWebHost WebHost { get; set; }

        internal HttpContext()
        {
            this.Request = new RequestBase();
            this.Response = new ResponseBase();
        }

        internal bool IsStaticsCached { get; set; }

        internal void Init(IWebHost webHost, IUserToken userToken, RequestDataReader requestDataReader)
        {
            this.WebHost = webHost;

            this.Request.Init(this.WebHost, userToken, requestDataReader);

            this.Response.Init(this.WebHost, userToken, this.Request.Protocal, webHost.WebConfig.IsZiped);

            this.Server = new HttpUtility(webHost.WebConfig.Root);

            IsStaticsCached = webHost.WebConfig.IsStaticsCached;
        }
        /// <summary>
        /// 执行用户自定义要处理的业务逻辑
        /// 比如这里就是Controller中内容
        /// </summary>
        /// <param name="routeTable"></param>
        internal void HttpHandler(RouteTable routeTable)
        {
            ActionResult result = null;

            switch (this.Request.Method)
            {
                case ConstHelper.GET:
                case ConstHelper.POST:

                    if (this.Request.Query.Count > 0)
                    {
                        foreach (var item in this.Request.Query)
                        {
                            this.Request.Parmas[item.Key]= item.Value;
                        }
                    }
                    if (this.Request.Forms.Count > 0)
                    {
                        foreach (var item in this.Request.Forms)
                        {
                            this.Request.Parmas[item.Key] = item.Value;
                        }
                    }
                    result = Invoker.Invoke(this, routeTable);
                    break;
                case ConstHelper.OPTIONS:
                    result = new EmptyResult();
                    break;
                default:
                    result = new ContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                    break;
            }
            this.Response.SetResult(result);
            this.Response.End();
        }

        public void Dispose()
        {
            Request?.Dispose();
            Response?.Dispose();
        }


    }
}
