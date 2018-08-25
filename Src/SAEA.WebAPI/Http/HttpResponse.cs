/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http
*文件名： HttpResponse
*版本号： V1.0.0.0
*唯一标识：2e43075f-a43d-4b60-bee1-1f9107e2d133
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 16:46:40
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 16:46:40
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Common;
using SAEA.WebAPI.Http.Base;
using SAEA.WebAPI.Http.Model;
using SAEA.WebAPI.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SAEA.WebAPI.Http
{
    public class HttpResponse : HttpBase, IDisposable
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;


        internal HttpServer HttpServer { get; set; }

        internal IUserToken UserToken { get; set; }

        internal HttpResponse()
        {
        }

        internal void Init(HttpServer httpServer, IUserToken userToken, string protocal)
        {
            this.HttpServer = httpServer;
            this.UserToken = userToken;

            this.Protocal = protocal;
        }

        /// <summary>
        /// 设置回复内容
        /// </summary>
        /// <param name="result"></param>
        internal void SetResult(ActionResult result)
        {
            this.Status = result.Status;
            if (result is EmptyResult)
            {
                return;
            }
            else if (result is FileResult)
            {
                var actionResult = result as FileResult;
                this.ContentType = actionResult.ContentType;
                this.SetContent(actionResult.Content);
            }
            else
            {
                this.ContentType = result.ContentType;
                this.SetContent(result.Content);
            }
        }

        internal HttpResponse SetContent(byte[] content, Encoding encoding = null)
        {
            this.Body = content;
            this.ContentLength = content.Length;
            return this;
        }

        internal HttpResponse SetContent(string content, Encoding encoding = null)
        {
            //初始化内容
            encoding = encoding != null ? encoding : Encoding.UTF8;
            return SetContent(encoding.GetBytes(content), encoding);
        }


        public string GetHeader(ResponseHeaderType header)
        {
            return base.GetHeader(header);
        }

        public void SetHeader(ResponseHeaderType header, string value)
        {
            base.SetHeader(header, value);
        }

        /// <summary>
        /// 构建响应头部
        /// </summary>
        /// <returns></returns>
        protected string BuildHeader()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(this.Protocal + SPACE + Status.ToNVString() + ENTER);
            builder.AppendLine("Server: Wenli's Server");
            builder.AppendLine("Keep-Alive: timeout=20");
            builder.AppendLine("Date: " + DateTimeHelper.Now.ToFString("r"));

            if (MvcApplication.IsZiped)
                //支持gzip
                builder.AppendLine("Content-Encoding:gzip");

            //支持跨域
            builder.AppendLine("Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS");
            builder.AppendLine("Access-Control-Allow-Origin: *");
            builder.AppendLine("Access-Control-Allow-Headers: Content-Type,X-Requested-With,Accept,yswenli");//可自行增加额外的header
            builder.AppendLine("Access-Control-Request-Methods: GET, POST, PUT, DELETE, OPTIONS");

            if (this.Headers != null && this.Headers.Count > 0)
            {
                foreach (var key in Headers.Keys)
                {
                    builder.AppendLine($"{key}: {Headers[key]}");
                }
            }
            var result = builder.ToString();
            builder.Clear();
            builder = null;
            return result;
        }

        /// <summary>
        /// 生成数据
        /// </summary>
        protected byte[] ToBytes()
        {
            List<byte> reponseDataList = new List<byte>();

            byte[] lineBytes = Encoding.UTF8.GetBytes(System.Environment.NewLine);

            var bdata = this.Body;
            if (MvcApplication.IsZiped && this.Body != null)
                bdata = GZipHelper.Compress(this.Body);

            var bodyLen = 0;

            if (bdata != null)
            {
                bodyLen = bdata.Length;
            }

            this.SetHeader(ResponseHeaderType.ContentLength, bodyLen.ToString());

            var header = BuildHeader();

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            //发送响应头
            reponseDataList.AddRange(headerBytes);
            //发送空行
            reponseDataList.AddRange(lineBytes);
            //发送内容
            if (bdata != null)
                reponseDataList.AddRange(bdata);

            var arr = reponseDataList.ToArray();
            this.Body = null;
            reponseDataList.Clear();
            reponseDataList = null;

            return arr;
        }


        public void Write(string str, Encoding encoding = null)
        {
            SetContent(str, encoding);
        }

        public void BinaryWrite(byte[] data, Encoding encoding = null)
        {
            SetContent(data, encoding);
        }



        public void End()
        {
            if (UserToken != null)
            {
                HttpServer.Reponse(UserToken, this.ToBytes());
            }
        }

        public void Dispose()
        {
            this.Headers.Clear();
            this.Write("");
        }

    }
}
