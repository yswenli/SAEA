/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Http
*文件名： HttpResponse
*版本号： V3.3.3.1
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
*版本号： V3.3.3.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SAEA.Http
{
    public class HttpResponse : HttpBase, IDisposable
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        internal IWebHost WebHost { get; set; }

        internal IUserToken UserToken { get; set; }

        bool _isZiped = false;

        internal HttpResponse()
        {

        }

        internal void Init(IWebHost webHost, IUserToken userToken, string protocal, bool isZiped = false)
        {
            this.WebHost = webHost;
            this.UserToken = userToken;

            this.Protocal = protocal;
            _isZiped = isZiped;
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
            builder.Append(this.Protocal + ConstHelper.SPACE + Status.ToNVString() + ConstHelper.ENTER);
            builder.AppendLine(ConstHelper.SERVERMVCSERVER);
            builder.AppendLine("Keep-Alive: timeout=20");
            builder.AppendLine("Date: " + DateTimeHelper.Now.ToFString("r"));

            if (_isZiped)
                //支持gzip
                builder.AppendLine("Content-Encoding:gzip");

            //支持跨域
            builder.AppendLine("Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS");
            builder.AppendLine("Access-Control-Allow-Origin: *");
            builder.AppendLine("Access-Control-Allow-Headers: Content-Type,X-Requested-With,Accept,yswenli");//可自行增加额外的header
            builder.AppendLine("Access-Control-Request-Methods: GET, POST, PUT, DELETE, OPTIONS");

            if (this.Headers.Count > 0)
            {
                foreach (var key in Headers.Keys)
                {
                    builder.AppendLine($"{key}: {Headers[key]}");
                }
            }
            var result = builder.ToString();
            builder.Clear();
            return result;
        }

        /// <summary>
        /// 设置回复内容
        /// </summary>
        /// <param name="result"></param>
        public void SetResult(IHttpResult result)
        {
            this.Status = result.Status;
            if (result is IEmptyResult)
            {
                return;
            }
            else if (result is IFileResult)
            {
                var fileResult = (IFileResult)result;
                this.ContentType = fileResult.ContentType;
                this.SetContent(fileResult.Content);
            }
            else
            {
                this.ContentType = result.ContentType;
                this.SetContent(result.Content);
            }
        }

        /// <summary>
        /// 生成数据
        /// </summary>
        protected byte[] ToBytes()
        {
            List<byte> reponseDataList = new List<byte>();

            byte[] lineBytes = Encoding.UTF8.GetBytes(System.Environment.NewLine);

            var bdata = this.Body;
            if (_isZiped && this.Body != null)
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
            WebHost.End(UserToken, this.ToBytes());
        }

        public void Dispose()
        {
            if (this.Query != null)
                this.Query.Clear();
            if (this.Forms != null)
                this.Forms.Clear();
            if (this.Parmas != null)
                this.Parmas.Clear();
            if (Body != null)
                Array.Clear(Body, 0, Body.Length);
        }

    }
}
