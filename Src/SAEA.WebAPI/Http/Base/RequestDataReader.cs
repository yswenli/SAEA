using SAEA.Common;
using SAEA.WebAPI.Common;
using SAEA.WebAPI.Http.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.WebAPI.Http.Base
{
    /// <summary>
    /// http字符串读取 
    /// </summary>
    internal class RequestDataReader : HttpBase, IDisposable
    {
        StringBuilder _stringBuilder = new StringBuilder();

        HttpUtility _httpUtility = new HttpUtility();
        /// <summary>
        /// 整个请求流头部结束字节位置
        /// </summary>
        public int Position
        {
            get; private set;
        }
        /// <summary>
        /// 接收到的文件信息
        /// </summary>
        public List<FilePart> PostFiles
        {
            get; set;
        }
        /// <summary>
        /// enctype="text/plain"
        /// </summary>
        public string Json
        {
            get; set;
        }

        /// <summary>
        /// http字符串读取
        /// </summary>
        public RequestDataReader()
        {

        }

        /// <summary>
        /// 分析request 头部
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public bool Analysis(byte[] buffer)
        {
            //获取headerStr
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                ms.Position = 0;

                using (SAEA.Common.StreamReader streamReader = new SAEA.Common.StreamReader(ms))
                {
                    while (true)
                    {
                        var str = streamReader.ReadLine();
                        if (str == string.Empty)
                        {
                            this.HeaderStr = _stringBuilder.ToString();
                            _stringBuilder.Clear();
                            break;
                        }
                        else if (str == null && string.IsNullOrEmpty(this.HeaderStr))
                        {
                            return false;

                        }
                        else
                            _stringBuilder.AppendLine(str);
                    }
                }
            }

            //分析requestHeader

            var rows = Regex.Split(this.HeaderStr, ENTER);

            var arr = Regex.Split(rows[0], @"(\s+)").Where(e => e.Trim() != string.Empty).ToArray();

            this.Method = arr[0];

            this.RelativeUrl = arr[1];

            if (this.RelativeUrl.Contains("?"))
            {
                var qarr = this.RelativeUrl.Split("?");
                this.Url = qarr[0];
                this.Query = GetRequestQuerys(qarr[1]);
            }
            else
            {
                this.Url = this.RelativeUrl;
            }

            var uarr = this.Url.Split("/");

            if (long.TryParse(uarr[uarr.Length - 1], out long id))
            {
                this.Url = this.Url.Substring(0, this.Url.LastIndexOf("/"));
                if (this.Query == null) this.Query = new Dictionary<string, string>();
                this.Query.Add(ConstHelper.ID, id.ToString());
            }

            this.Protocal = arr[2];

            this.Headers = GetRequestHeaders(rows);

            if (this.Headers != null)
            {
                //cookies
                var cookiesStr = string.Empty;
                if (this.Headers.TryGetValue(RequestHeaderType.Cookie.GetDescription(), out cookiesStr))
                {
                    this.Cookies = GetCookies(cookiesStr);
                }

                //post数据分析
                if (this.Method == ConstString.POSTStr)
                {
                    using (MemoryStream ms = new MemoryStream(buffer))
                    {
                        var poistion = ms.Position = 0;
                        while (true)
                        {
                            if (ms.ReadByte() == 13 && ms.ReadByte() == 10 && ms.ReadByte() == 13 && ms.ReadByte() == 10)
                            {
                                poistion = ms.Position;
                                break;
                            }
                        }
                        this.Position = (int)poistion;
                    }

                    string contentTypeStr = string.Empty;
                    if (this.Headers.TryGetValue(RequestHeaderType.ContentType.GetDescription(), out contentTypeStr))
                    {
                        //form-data
                        if (contentTypeStr.IndexOf(ConstString.FORMENCTYPE2) > -1)
                        {
                            this.IsFormData = true;

                            this.Boundary = "--" + Regex.Split(contentTypeStr, ";")[1].Replace(ConstHelper.BOUNDARY, "");
                        }
                    }
                    string contentLengthStr = string.Empty;
                    if (this.Headers.TryGetValue(RequestHeaderType.ContentLength.GetDescription(), out contentLengthStr))
                    {
                        int cl = 0;

                        if (int.TryParse(contentLengthStr, out cl))
                        {
                            this.ContentLength = cl;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 分析request body
        /// </summary>
        /// <param name="requestData"></param>
        public void AnalysisBody(byte[] requestData)
        {
            var contentLen = this.ContentLength;
            if (contentLen > 0)
            {
                var positon = this.Position;
                var totlalLen = contentLen + positon;
                this.Body = new byte[contentLen];
                Buffer.BlockCopy(requestData, positon, this.Body, 0, this.Body.Length);

                switch (this.ContentType)
                {
                    case ConstString.FORMENCTYPE2:
                        using (MemoryStream ms = new MemoryStream(this.Body))
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
                                        if (str.IndexOf(ConstHelper.CT) > -1)
                                        {
                                            var filePart = GetRequestFormsWithMultiPart(sb.ToString());

                                            if (filePart != null)
                                            {
                                                sr.ReadLine();

                                                filePart.Data = sr.ReadData(sr.Position, this.Boundary);
                                                if (filePart.Data != null)
                                                {
                                                    filePart.Data = filePart.Data.Take(filePart.Data.Length - 2).ToArray();
                                                }
                                                if (this.PostFiles == null)
                                                    this.PostFiles = new List<FilePart>();
                                                this.PostFiles.Add(filePart);
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
                    case ConstString.FORMENCTYPE3:
                        this.Json = Encoding.UTF8.GetString(this.Body);
                        break;
                    default:
                        this.Forms = GetRequestForms(Encoding.UTF8.GetString(this.Body));
                        break;
                }
            }
        }
        #region private

        private Dictionary<string, string> GetRequestQuerys(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = Regex.Split(row, "&");
            if (kvs == null || kvs.Count() <= 0) return null;
            return kvs.ToDictionary(e => Regex.Split(e, "=")[0], e => _httpUtility.UrlDecode(Regex.Split(e, "=")[1]));
        }

        private Dictionary<string, string> GetRequestForms(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = Regex.Split(row, "&");
            if (kvs == null || kvs.Count() <= 0) return null;
            return kvs.ToDictionary(e => Regex.Split(e, "=")[0], e => _httpUtility.HtmlDecode(_httpUtility.UrlDecode(Regex.Split(e, "=")[1])));
        }

        private Dictionary<string, string> GetRequestHeaders(IEnumerable<string> rows)
        {
            if (rows == null || rows.Count() <= 0) return null;
            var target = rows.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(e => e.Value.Trim() == string.Empty);
            var length = target == null ? rows.Count() - 1 : target.Index;
            if (length <= 1) return null;
            var range = Enumerable.Range(1, length - 1);
            return range.Select(e => rows.ElementAt(e)).Distinct().ToDictionary(e => e.Split(':')[0], e => e.Split(':')[1].Trim());
        }

        private Dictionary<string, string> GetCookies(string cookieStr)
        {
            if (string.IsNullOrEmpty(cookieStr)) return null;
            var kvs = Regex.Split(cookieStr, ";");
            if (kvs == null || kvs.Count() <= 0) return null;
            return kvs.ToDictionary(e => Regex.Split(e, "=")[0], e => Regex.Split(e, "=")[1]);
        }

        private string GetRequestBody(IEnumerable<string> rows)
        {
            var target = rows.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(e => e.Value.Trim() == string.Empty);
            if (target == null) return null;
            var range = Enumerable.Range(target.Index + 1, rows.Count() - target.Index - 1);
            return string.Join(Environment.NewLine, range.Select(e => rows.ElementAt(e)).ToArray());
        }

        private FilePart GetRequestFormsWithMultiPart(string row)
        {
            FilePart filePart = null;

            var sections = row.Split(this.Boundary, StringSplitOptions.RemoveEmptyEntries);

            if (sections != null)
            {
                foreach (var section in sections)
                {
                    if (section.IndexOf(ConstHelper.CT) > -1)
                    {
                        var arr = section.Split(ENTER, StringSplitOptions.RemoveEmptyEntries);
                        if (arr != null && arr.Length > 0)
                        {
                            var firsts = Regex.Split(arr[0], ";");
                            var name = Regex.Split(firsts[1], "=")[1].Replace("\"", "");
                            var fileName = Regex.Split(firsts[2], "=")[1].Replace("\"", "");
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
                        var arr = section.Split(ENTER, StringSplitOptions.RemoveEmptyEntries);
                        if (arr != null)
                        {
                            if (arr.Length > 0 && arr[0].IndexOf(";") > -1 && arr[0].IndexOf("=") > -1)
                            {
                                var lineArr = Regex.Split(arr[0], ";");
                                if (lineArr == null) continue;
                                var name = Regex.Split(lineArr[1], "=")[1].Replace("\"", "");
                                var value = string.Empty;

                                if (arr.Length > 1)
                                {
                                    value = arr[1];
                                }
                                if (this.Forms == null) this.Forms = new Dictionary<string, string>();
                                this.Forms.TryAdd(name, value);
                            }
                        }
                    }
                }
            }

            return filePart;
        }
        #endregion



        public void Dispose()
        {
            if (this.Query != null)
                this.Query.Clear();
            if (this.Forms != null)
                this.Forms.Clear();
            if (this.Parmas != null)
                this.Parmas.Clear();
            _stringBuilder.Clear();
            if (Body != null)
                Array.Clear(Body, 0, Body.Length);
            Body = null;
        }
    }
}
