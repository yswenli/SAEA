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
using SAEA.Commom;
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Http.Base;
using SAEA.WebAPI.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SAEA.WebAPI.Http
{
    public class HttpResponse : BaseHeader
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        byte[] _content = null;


        internal HttpServer HttpServer { get; set; }

        internal IUserToken UserToken { get; set; }

        internal HttpResponse()
        {

        }

        internal void Init(HttpServer httpServer, IUserToken userToken)
        {
            this.HttpServer = httpServer;
            this.UserToken = userToken;
        }

        /// <summary>
        /// 设置回复内容
        /// </summary>
        /// <param name="result"></param>
        internal void SetResult(ActionResult result)
        {
            this.Content_Encoding = result.ContentEncoding.EncodingName;
            this.Content_Type = result.ContentType;
            this.Status = result.Status;

            if (result is EmptyResult)
            {
                return;
            }

            if (result is FileResult)
            {
                var f = result as FileResult;

                this.SetContent(f.Content);

                return;
            }

            this.SetContent(result.Content);
        }

        internal HttpResponse SetContent(byte[] content, Encoding encoding = null)
        {
            this._content = content;
            this.Encoding = encoding != null ? encoding : Encoding.UTF8;
            this.Content_Length = content.Length.ToString();
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
            builder.Append(Protocols + SPACE + Status.ToNVString() + ENTER);
            builder.AppendLine("Server: Wenli's Server");
            builder.AppendLine("Keep-Alive: timeout=20");
            builder.AppendLine("Date: " + DateTimeHelper.Now.ToFString("r"));

            if (!string.IsNullOrEmpty(this.Content_Type))
                builder.AppendLine("Content-Type:" + this.Content_Type);

            //支持跨域
            builder.AppendLine("Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS");
            builder.AppendLine("Access-Control-Allow-Origin: *");
            builder.AppendLine("Access-Control-Allow-Headers: Content-Type,X-Requested-With,Accept,yswenli");//可自行增加额外的header
            builder.AppendLine("Access-Control-Request-Methods: GET, POST, PUT, DELETE, OPTIONS");

            if (this.Headers != null && this.Headers.Count > 0)
            {
                foreach (var key in Headers.Names)
                {
                    builder.AppendLine($"{key}: {Headers[key]}");
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 生成数据
        /// </summary>
        protected byte[] ToBytes()
        {
            List<byte> list = new List<byte>();
            //发送响应头
            var header = BuildHeader();
            byte[] headerBytes = this.Encoding.GetBytes(header);
            list.AddRange(headerBytes);

            //发送空行
            byte[] lineBytes = this.Encoding.GetBytes(System.Environment.NewLine);
            list.AddRange(lineBytes);

            //发送内容
            list.AddRange(_content);

            return list.ToArray();
        }


        public void Write(string str, Encoding encoding = null)
        {
            SetContent(str, encoding);
        }

        public void BinaryWrite(byte[] data, Encoding encoding = null)
        {
            SetContent(data, encoding);
        }

        public void Clear()
        {
            this.Headers.Clear();
            this.Write("");
        }

        public void End()
        {
            if (UserToken != null)
            {
                HttpServer.Replay(UserToken, this.ToBytes());
                HttpServer.Close(UserToken);
            }
        }

    }
}
