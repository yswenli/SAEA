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
using SAEA.WebAPI.Http.Base;
using SAEA.WebAPI.Mvc;
using System.IO;
using System.Text;

namespace SAEA.WebAPI.Http
{
    /// <summary>
    /// http上下文
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

        internal HttpContext()
        {
            this.Request = new HttpRequest();
            this.Response = new HttpResponse();
        }

        internal void Init(HttpServer httpServer, IUserToken userToken, string htmlStr)
        {
            this.Request.Init(httpServer, userToken, htmlStr);

            this.Response.Init(httpServer, userToken);
        }

        internal void InvokeAction()
        {
            var result = AreaCollection.Invoke(this, this.Request.URL, this.Request.Params, this.Request.Method == "POST");

            if (!(result is EmptyResult))
            {
                this.Response.SetResult(result);
            }
            this.Response.End();
        }

        internal void Free()
        {
            if (this.Request != null)
            {
                this.Request.Clear();
            }
            if (this.Response != null)
            {
                this.Response.Clear();
            }
        }


    }
}
