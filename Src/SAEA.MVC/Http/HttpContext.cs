/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http
*文件名： HttpContext
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using Newtonsoft.Json.Linq;
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Http.Base;
using SAEA.WebAPI.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Text;
using SAEA.Common;

namespace SAEA.WebAPI.Http
{
    /// <summary>
    /// http上下文
    /// </summary>
    public class HttpContext : IDisposable
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
        } = new HttpUtility();

        internal HttpContext()
        {
            this.Request = new HttpRequest();
            this.Response = new HttpResponse();
        }

        internal void Init(HttpServer httpServer, IUserToken userToken, RequestDataReader requestDataReader)
        {
            this.Request.Init(httpServer, userToken, requestDataReader);

            this.Response.Init(httpServer, userToken, this.Request.Protocal);
        }
        /// <summary>
        /// 执行用户自定义要处理的业务逻辑
        /// 比如这里就是Controller中内容
        /// </summary>
        internal void HttpHandler()
        {
            ActionResult result = null;

            switch (this.Request.Method)
            {
                case ConstString.GETStr:
                case ConstString.POSTStr:

                    if (this.Request.Parmas == null) this.Request.Parmas = new System.Collections.Generic.Dictionary<string, string>();

                    if (this.Request.Query != null && this.Request.Query.Count > 0)
                    {
                        foreach (var item in this.Request.Query)
                        {
                            this.Request.Parmas.TryAdd(item.Key, item.Value);
                        }
                    }
                    if (this.Request.Forms != null && this.Request.Forms.Count > 0)
                    {
                        foreach (var item in this.Request.Forms)
                        {
                            this.Request.Parmas.TryAdd(item.Key, item.Value);
                        }
                    }
                    result = AreaCollection.Invoke(this, this.Request.Url, this.Request.Parmas.ToNameValueCollection(), this.Request.Method == "POST");
                    break;
                case ConstString.OPTIONSStr:
                    result = new EmptyResult();
                    break;
                default:
                    result = new ContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                    break;
            }
            if (!(result is EmptyResult))
            {
                this.Response.SetResult(result);
            }
            this.Response.End();
        }

        public void Dispose()
        {
            Request?.Dispose();
            Response?.Dispose();
        }


    }
}
