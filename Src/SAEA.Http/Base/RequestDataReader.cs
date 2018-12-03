/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Base
*文件名： RequestDataReader
*版本号： V3.3.3.5
*唯一标识：01f783cd-c751-47c5-a5b9-96d3aa840c70
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/16 11:03:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/16 11:03:29
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.Http.Base
{
    /// <summary>
    /// http字符串读取 
    /// </summary>
    public static class RequestDataReader
    {
        static readonly byte[] _dEnterBytes = new byte[] { 13, 10, 13, 10 };

        /// <summary>
        /// 分析request 头部
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="httpMessage"></param>
        /// <returns></returns>
        public static bool Analysis(byte[] buffer, out HttpMessage httpMessage)
        {
            httpMessage = null;

            var bufferSpan = buffer.AsSpan();

            var count = bufferSpan.Length;

            var index = bufferSpan.IndexOf(_dEnterBytes);

            if (index == count - 4)
            {
                httpMessage = new HttpMessage();
                httpMessage.HeaderStr = Encoding.UTF8.GetString(buffer);
                httpMessage.Position = count;
            }
            else
            {
                if (index > 0)
                {
                    httpMessage = new HttpMessage();
                    httpMessage.HeaderStr = Encoding.UTF8.GetString(bufferSpan.Slice(0, index + 4).ToArray());
                    httpMessage.Position = index + 4;
                }
                if (httpMessage == null) return false;
            }

            //分析requestHeader

            var rows = httpMessage.HeaderStr.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);

            var arr = rows[0].Split(ConstHelper.SPACE);

            httpMessage.Method = arr[0];

            httpMessage.RelativeUrl = arr[1];

            httpMessage.Protocal = arr[2];

            if (httpMessage.RelativeUrl.Contains("?"))
            {
                var qarr = httpMessage.RelativeUrl.Split("?");
                httpMessage.Url = qarr[0];
                httpMessage.Query = GetRequestQuerys(qarr[1]);
            }
            else
            {
                httpMessage.Url = httpMessage.RelativeUrl;
            }

            var uarr = httpMessage.Url.Split("/");

            if (long.TryParse(uarr[uarr.Length - 1], out long id))
            {
                httpMessage.Url = StringHelper.Substring(httpMessage.Url, 0, httpMessage.Url.LastIndexOf("/"));
                httpMessage.Query.Add(ConstHelper.ID, id.ToString());
            }

            var lastRows = rows.AsSpan().Slice(1).ToArray();

            httpMessage.Headers = GetRequestHeaders(lastRows);

            //cookies
            var cookiesStr = string.Empty;
            if (httpMessage.Headers.TryGetValue(RequestHeaderType.Cookie.GetDescription(), out cookiesStr))
            {
                httpMessage.Cookies = GetCookies(cookiesStr);
            }

            //post数据分析
            if (httpMessage.Method == ConstHelper.POST)
            {
                string contentTypeStr = string.Empty;
                if (httpMessage.Headers.TryGetValue(RequestHeaderType.ContentType.GetDescription(), out contentTypeStr))
                {
                    //form-data
                    if (contentTypeStr.IndexOf(ConstHelper.FORMENCTYPE2) > -1)
                    {
                        httpMessage.IsFormData = true;

                        httpMessage.Boundary = "--" + Regex.Split(contentTypeStr, ";")[1].Replace(ConstHelper.BOUNDARY, "");
                    }
                }
                string contentLengthStr = string.Empty;
                if (httpMessage.Headers.TryGetValue(RequestHeaderType.ContentLength.GetDescription(), out contentLengthStr))
                {
                    int cl = 0;

                    if (int.TryParse(contentLengthStr, out cl))
                    {
                        httpMessage.ContentLength = cl;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 分析request body
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="httpMessage"></param>
        public static void AnalysisBody(byte[] requestData, HttpMessage httpMessage)
        {
            var contentLen = httpMessage.ContentLength;
            if (contentLen > 0)
            {
                var positon = httpMessage.Position;
                var totlalLen = contentLen + positon;
                httpMessage.Body = new byte[contentLen];
                Buffer.BlockCopy(requestData, positon, httpMessage.Body, 0, httpMessage.Body.Length);

                switch (httpMessage.ContentType)
                {
                    case ConstHelper.FORMENCTYPE2:
                        using (MemoryStream ms = new MemoryStream(httpMessage.Body))
                        {
                            ms.Position = 0;
                            using (var sr = new SAEA.Common.StreamReader(ms))
                            {
                                StringBuilder sb = new StringBuilder();
                                var str = string.Empty;
                                do
                                {
                                    str = sr.ReadLine();
                                    if (str == null)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        sb.AppendLine(str);
                                        if (str.Contains(ConstHelper.CT))
                                        {
                                            var filePart = GetRequestFormsWithMultiPart(sb.ToString(), httpMessage);

                                            if (filePart != null)
                                            {
                                                sr.ReadLine();

                                                filePart.Data = sr.ReadData(sr.Position, httpMessage.Boundary);
                                                if (filePart.Data != null)
                                                {
                                                    filePart.Data = filePart.Data.Take(filePart.Data.Length - 2).ToArray();
                                                }
                                                httpMessage.PostFiles.Add(filePart);
                                            }
                                            sb.Clear();
                                            sr.ReadLine();
                                        }
                                    }
                                }
                                while (true);

                            }
                        }
                        break;
                    case ConstHelper.FORMENCTYPE3:
                        httpMessage.Json = Encoding.UTF8.GetString(httpMessage.Body);
                        break;
                    default:
                        httpMessage.Forms = GetRequestForms(Encoding.UTF8.GetString(httpMessage.Body));
                        break;
                }
            }
        }
        #region private

        private static Dictionary<string, string> GetRequestQuerys(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = row.Split("&", StringSplitOptions.RemoveEmptyEntries);
            if (kvs == null || kvs.Count() <= 0) return null;
            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (var item in kvs)
            {
                var arr = item.Split("=");
                dic[arr[0]] = HttpUtility.UrlDecode(arr[1]);
            }
            return dic;
        }

        private static Dictionary<string, string> GetRequestForms(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = row.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);
            if (kvs == null || kvs.Count() <= 0) return null;
            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (var item in kvs)
            {
                var arr = item.Split("=");
                dic[arr[0]] = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(arr[1]));
            }
            return dic;
        }

        private static Dictionary<string, string> GetRequestHeaders(IEnumerable<string> rows)
        {
            var result = new Dictionary<string, string>();
            if (rows == null || rows.Count() <= 0) return result;
            foreach (var row in rows)
            {
                var rowArr = row.Split(":");
                result[rowArr[0]] = rowArr[1].Trim();
            }
            return result;
        }

        private static Dictionary<string, string> GetCookies(string cookieStr)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(cookieStr)) return result;

            var rows = cookieStr.Split(";", StringSplitOptions.RemoveEmptyEntries);

            if (rows == null || rows.Count() <= 0) return result;

            foreach (var row in rows)
            {
                var rowArr = row.Split(":");
                result[rowArr[0]] = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(rowArr[1]));
            }
            return result;
        }

        private static FilePart GetRequestFormsWithMultiPart(string row, HttpMessage httpMessage)
        {
            FilePart filePart = null;

            var sections = row.Split(httpMessage.Boundary, StringSplitOptions.RemoveEmptyEntries);

            if (sections != null)
            {
                foreach (var section in sections)
                {
                    if (section.IndexOf(ConstHelper.CT) > -1)
                    {
                        var arr = section.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);
                        if (arr != null && arr.Length > 0)
                        {
                            var firsts = arr[0].Split(";");
                            var name = firsts[1].Split("=")[1].Replace("\"", "");
                            var fileName = firsts[2].Split("=")[1].Replace("\"", "");
                            var type = string.Empty;
                            if (arr.Length > 1)
                            {
                                type = arr[1].Split(new[] { ";", ":" }, StringSplitOptions.RemoveEmptyEntries)[1];
                            }
                            filePart = new FilePart(name, fileName, type);
                        }
                    }
                    else
                    {
                        var arr = section.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);
                        if (arr != null)
                        {
                            if (arr.Length > 0 && arr[0].IndexOf(";") > -1 && arr[0].IndexOf("=") > -1)
                            {
                                var lineArr = arr[0].Split(";");
                                if (lineArr == null) continue;
                                var name = lineArr[1].Split("=")[1].Replace("\"", "");
                                var value = string.Empty;

                                if (arr.Length > 1)
                                {
                                    value = arr[1];
                                }
                                httpMessage.Forms[name] = value;
                            }
                        }
                    }
                }
            }

            return filePart;
        }
        #endregion
    }
}
