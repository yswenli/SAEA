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
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Common;
using SAEA.WebAPI.Http;
using SAEA.WebAPI.Http.Base;
using SAEA.WebAPI.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.WebAPI.Http
{
    /// <summary>
    /// MVC上下文
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

        public HttpServerUtilityBase Server
        {
            get;
            private set;
        } = new HttpServerUtilityBase();

        internal static HttpContext CreateInstance(HttpServer httpServer, IUserToken userToken, string htmlStr)
        {
            var buffer = Encoding.UTF8.GetBytes(htmlStr);

            var ms = new MemoryStream(buffer);

            var httpRequest = HttpRequest.CreateInstance(httpServer, userToken, ms);

            var httpContext = new HttpContext(httpRequest);

            return httpContext;
        }

        public HttpContext(HttpRequest request)
        {
            this.Request = request;

            var response = HttpResponse.CreateInstance(this.Request.HttpServer, this.Request.UserToken);

            this.Response = response;

            var result = AreaCollection.Invoke(this, request.URL, request.Params, this.Request.Method == "POST");

            if(!(result is EmptyResult))
            {
                HttpResponse.SetResult(response, result);
            }
        }


    }
}
