/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpResponse
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SAEA.Http
{
    public class HttpResponse : HttpBase, IDisposable
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        internal IWebHost WebHost { get; set; }

        bool _isZiped = false;

        IUserToken UserToken { get; set; }

        internal HttpResponse()
        {

        }

        internal void Init(IWebHost webHost, IUserToken userToken, string protocal, bool isZiped = false)
        {
            WebHost = webHost;
            UserToken = userToken;
            Protocal = protocal;
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
            builder.AppendLine($"{this.Protocal} {Status.ToNVString()}");
            builder.AppendLine(ConstHelper.ServerName);
            builder.AppendLine("Connection: close");
            builder.AppendLine("Date: " + DateTimeHelper.Now.ToGMTString());

            if (_isZiped)
                //支持gzip
                builder.AppendLine(ConstHelper.ContentEncoding);

            //支持跨域
            builder.Append(ConstHelper.CrossDomain);

            if (this.Headers.Count > 0)
            {
                foreach (var key in Headers.Keys)
                {
                    builder.AppendLine($"{key}: {Headers[key]}");
                }
            }
            if (this.Cookies.Count > 0)
            {
                builder.Append(this.Cookies.ToString());
            }

            var result = builder.ToString();
            builder.Clear();
            return result;
        }

        /// <summary>
        /// 设置回复内容
        /// </summary>
        /// <param name="result"></param>
        public void SetCached(IHttpResult result, string cacheCalcResult = "-1,-1")
        {
            this.Status = result.Status;

            #region cached

            var carr = cacheCalcResult.Split(",").Select(b => Convert.ToInt32(b)).ToArray();

            if (carr.Length == 2 && carr[0] >= 0)
            {
                this.SetHeader(ResponseHeaderType.CacheControl, "Max-Age=" + carr[1]);
                this.SetHeader(ResponseHeaderType.Expires, DateTimeHelper.Now.AddSeconds(carr[1]).ToGMTString());
                this.Status = HttpStatusCode.OK;

                if (carr[0] == 0)
                {
                    this.Headers["Last-Modified"] = DateTime.Now.ToGMTString();
                }

                if (carr[0] == 1)
                {
                    if (carr[1] > 0)
                    {
                        this.Status = HttpStatusCode.NotModified;

                        return;
                    }
                }
            }

            #endregion

            if (result is IEmptyResult || result is IBigDataResult)
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

        /// <summary>
        /// 此方法为基础输出方法，使用时请设置status和contenttype
        /// </summary>
        /// <param name="str"></param>
        /// <param name="encoding"></param>
        public void Write(string str, Encoding encoding = null)
        {
            SetContent(str, encoding);
        }

        /// <summary>
        /// 此方法为基础输出方法，使用时请设置status和contenttype
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encoding"></param>
        public void BinaryWrite(byte[] data, Encoding encoding = null)
        {
            SetContent(data, encoding);
        }

        #region 针对类似大文件场景分开处理

        /// <summary>
        /// 仅发送头，
        /// 与SendData，SendEnd联合使用
        /// </summary>
        /// <param name="bodyLen"></param>
        /// <param name="encoding"></param>
        public void SendHeader(long bodyLen, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;

            List<byte> reponseDataList = new List<byte>();

            byte[] lineBytes = encoding.GetBytes(System.Environment.NewLine);

            this.SetHeader(ResponseHeaderType.ContentLength, bodyLen.ToString());

            var header = BuildHeader();

            byte[] headerBytes = encoding.GetBytes(header);

            //发送响应头
            reponseDataList.AddRange(headerBytes);
            //发送空行
            reponseDataList.AddRange(lineBytes);

            var arr = reponseDataList.ToArray();

            reponseDataList.Clear();

            WebHost.Send(UserToken, arr);
        }


        /// <summary>
        /// 发送数据，
        /// 与SendHeader，SendEnd联合使用
        /// </summary>
        /// <param name="data"></param>
        public void SendData(byte[] data)
        {
            WebHost.Send(UserToken, data);
        }

        /// <summary>
        /// 发送结束，
        /// 与SendHeader，SendData联合使用
        /// </summary>
        public void SendEnd()
        {
            WebHost.Disconnect(UserToken);
            this.Body = null;
        }

        #endregion


        /// <summary>
        /// 结束当前流程
        /// </summary>
        public void End()
        {
            WebHost.End(UserToken, this.ToBytes());
        }

        /// <summary>
        /// 释放资源
        /// </summary>
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
