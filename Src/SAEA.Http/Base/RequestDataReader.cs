﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： RequestDataReader
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
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
        /// <param name="bufferSpan"></param>
        /// <param name="httpMessage"></param>
        /// <returns></returns>
        public static int Analysis(Span<byte> bufferSpan, out HttpMessage httpMessage)
        {
            httpMessage = null;

            var length = bufferSpan.Length;

            var index = bufferSpan.IndexOf(_dEnterBytes);

            if (index < 4) return 0;

            if (index == length - 4)
            {
                httpMessage = new HttpMessage();
                httpMessage.HeaderStr = Encoding.ASCII.GetString(bufferSpan.ToArray());
                httpMessage.Position = length;
            }
            else
            {
                if (index > 0 && bufferSpan.Length >= index + 4)
                {
                    httpMessage = new HttpMessage();
                    httpMessage.HeaderStr = Encoding.ASCII.GetString(bufferSpan.Slice(0, index + 4).ToArray());
                    httpMessage.Position = index + 4;
                }
            }

            if (httpMessage == null) return 0;

            //分析requestHeader

            var rows = httpMessage.HeaderStr.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);

            var arr = rows[0].Split(ConstHelper.SPACE);

            httpMessage.Method = arr[0];

            httpMessage.RelativeUrl = arr[1];

            httpMessage.Protocal = arr[2];

            if (httpMessage.RelativeUrl.Contains(ConstHelper.QUESTIONMARK))
            {
                var qarr = httpMessage.RelativeUrl.Split(ConstHelper.QUESTIONMARK);
                httpMessage.Url = qarr[0];
                if (qarr.Length > 1)
                    httpMessage.Query = GetRequestQuerys(qarr[1]);
            }
            else
            {
                httpMessage.Url = httpMessage.RelativeUrl;
            }

            var uarr = httpMessage.Url.Split(ConstHelper.SLASH);

            if (long.TryParse(uarr[uarr.Length - 1], out long id))
            {
                httpMessage.Url = StringHelper.Substring(httpMessage.Url, 0, httpMessage.Url.LastIndexOf(ConstHelper.SLASH));
                httpMessage.Query.Add(ConstHelper.ID, id.ToString());
            }

            var lastRows = rows.AsSpan().Slice(1).ToArray();

            httpMessage.Headers = GetRequestHeaders(lastRows);

            //cookies
            if (httpMessage.Headers.TryGetValue(RequestHeaderType.Cookie.GetDescription(), out string cookiesStr))
            {
                httpMessage.Cookies = HttpCookies.Parse(cookiesStr);
            }

            //post数据分析
            if (httpMessage.Method == ConstHelper.POST)
            {
                //form-data
                if (httpMessage.ContentType != null && httpMessage.ContentType.AsSpan().Contains(ConstHelper.FormData.AsSpan(), StringComparison.InvariantCultureIgnoreCase))
                {
                    httpMessage.IsFormData = true;

                    httpMessage.Boundary = "--" + Regex.Split(httpMessage.ContentType, ConstHelper.SEMICOLON)[1].Replace(ConstHelper.BOUNDARY, "");
                }

                if (httpMessage.Headers.TryGetValue(RequestHeaderType.ContentLength.GetDescription(), out string contentLengthStr))
                {
                    if (int.TryParse(contentLengthStr, out int cl))
                    {
                        httpMessage.ContentLength = cl;
                    }
                }
            }
            return index + 4;
        }

        /// <summary>
        /// 分析request body
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="httpMessage"></param>
        public static bool AnalysisBody(byte[] requestData, HttpMessage httpMessage)
        {
            var contentLen = httpMessage.ContentLength;
            var positon = httpMessage.Position;
            try
            {
                if (requestData.Length >= positon + contentLen)
                {
                    httpMessage.Body = requestData.AsSpan().Slice(positon, contentLen).ToArray();

                    if (httpMessage.ContentType.IndexOf(ConstHelper.FormUrlEncode, StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        httpMessage.Forms = GetRequestForms(Encoding.UTF8.GetString(httpMessage.Body));
                    }
                    else if (httpMessage.ContentType.IndexOf(ConstHelper.FormData, StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        using (MemoryStream ms = new MemoryStream(httpMessage.Body))
                        {
                            ms.Position = 0;
                            using var sr = new SAEA.Common.IO.StreamReader(ms);
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
                    else if (httpMessage.ContentType.IndexOf(ConstHelper.Json, StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        httpMessage.Json = Encoding.UTF8.GetString(httpMessage.Body);
                    }
                    return true;
                }
            }
            catch
            {
                
            }
            return false;
        }
        #region private

        private static Dictionary<string, string> GetRequestQuerys(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = row.Split(ConstHelper.AMPERSAND, StringSplitOptions.RemoveEmptyEntries);
            if (kvs == null || kvs.Count() <= 0) return null;
            Dictionary<string, string> dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in kvs)
            {
                var index = item.IndexOf("=");

                var key = item.Substring(0, index);

                var value = string.Empty;

                if (item.Length > index + 1)
                {
                    value = HttpUtility.UrlDecode(item.Substring(index + 1));
                }

                dic[key] = value;
            }
            return dic;
        }

        private static Dictionary<string, string> GetRequestForms(string row)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(row)) return dic;

            if (row.IndexOf(ConstHelper.ENTER) > 0 && row.TrimStart().IndexOf("{") != 0)
            {
                var kvs = row.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);
                if (kvs == null || kvs.Count() <= 0) return dic;
                foreach (var item in kvs)
                {
                    var key = item.Substring(0, item.IndexOf(ConstHelper.EQUO));
                    var value = item.Substring(item.IndexOf(ConstHelper.EQUO) + 1);
                    dic[key] = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(value));
                }
            }
            else if (row.IndexOf("&") > 0)
            {
                var kvs = row.Split(ConstHelper.AMPERSAND, StringSplitOptions.RemoveEmptyEntries);
                if (kvs == null || kvs.Count() <= 0) return null;
                foreach (var item in kvs)
                {
                    var arr = item.Split(ConstHelper.EQUO);
                    dic[arr[0]] = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(arr[1]));
                }
            }
            else if (row.IndexOf(ConstHelper.EQUO) > 0)
            {
                var arr = row.Split(ConstHelper.EQUO);
                dic[arr[0]] = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(arr[1]));
            }
            return dic;
        }


        private static Dictionary<string, string> GetRequestHeaders(IEnumerable<string> rows)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (rows == null || rows.Count() <= 0) return result;

            foreach (var row in rows)
            {
                var rowArr = row.Split(ConstHelper.COLON);
                if (rowArr.Length == 2)
                    result[rowArr[0]] = rowArr[1].Trim();
                else
                    result[rowArr[0]] = "";
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
                            var firsts = arr[0].Split(ConstHelper.SEMICOLON);
                            var name = firsts[1].Split(ConstHelper.EQUO)[1].Replace("\"", "");
                            var fileName = firsts[2].Split(ConstHelper.EQUO)[1].Replace("\"", "");
                            var type = string.Empty;
                            if (arr.Length > 1)
                            {
                                type = arr[1].Split(new[] { ConstHelper.SEMICOLON, ConstHelper.COLON }, StringSplitOptions.RemoveEmptyEntries)[1];
                            }
                            filePart = new FilePart(name, fileName, type);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(section))
                        {
                            var content = section.Trim();

                            var name = content.Substring(content.IndexOf("=\"") + 2);

                            name = name.Substring(0, name.LastIndexOf(ConstHelper.DOUBLEQUOTES));

                            var value = content.Substring(content.IndexOf(ConstHelper.DENTER) + 4);

                            httpMessage.Forms[name] = value;
                        }
                    }
                }
            }

            return filePart;
        }
        #endregion
    }
}
