/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： RequestDataReader
*版本号： v26.4.23.1
*唯一标识：90423416-7865-4de6-b980-31be327f93eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：RequestDataReader类
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:44:02
*修改人： yswenli
*版本号： v26.11.16.1
*描述：multipart 表单识别改为按正文长度与内容类型判断，PUT/PATCH 携带的表单同样解析
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Model;

using System;
using System.Collections.Generic;
using System.IO;
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
                httpMessage.HeaderStr = GetAsciiString(bufferSpan);
                httpMessage.Position = length;
            }
            else
            {
                if (index > 0 && bufferSpan.Length >= index + 4)
                {
                    httpMessage = new HttpMessage();
                    httpMessage.HeaderStr = GetAsciiString(bufferSpan.Slice(0, index + 4));
                    httpMessage.Position = index + 4;
                }
            }

            if (httpMessage == null) return 0;

            //分析requestHeader

            var headerStr = httpMessage.HeaderStr;

            // 请求行前允许有空行，跳过前导的完整 CRLF 对（只跳成对的，单独的 LF 留在请求行里）
            var requestLineStart = 0;

            while (requestLineStart + 1 < headerStr.Length && headerStr[requestLineStart] == '\r' && headerStr[requestLineStart + 1] == '\n')
                requestLineStart += 2;

            var lineEnd = headerStr.IndexOf(ConstHelper.ENTER, requestLineStart, StringComparison.Ordinal);

            if (lineEnd < 0) lineEnd = headerStr.Length;

            ParseRequestLine(httpMessage, headerStr, requestLineStart, lineEnd);

            var questionIndex = httpMessage.RelativeUrl.IndexOf(ConstHelper.QUESTIONMARK);

            if (questionIndex > -1)
            {
                var target = httpMessage.RelativeUrl;

                httpMessage.Url = target.Substring(0, questionIndex);

                // 原实现取 Split('?') 的 qarr[1]，即首个与第二个 '?' 之间的部分
                var nextQuestionIndex = target.IndexOf(ConstHelper.QUESTIONMARK, questionIndex + 1);

                var queryEnd = nextQuestionIndex < 0 ? target.Length : nextQuestionIndex;

                httpMessage.Query = GetRequestQuerys(target.Substring(questionIndex + 1, queryEnd - questionIndex - 1));
            }
            else
            {
                httpMessage.Url = httpMessage.RelativeUrl;
            }

            var lastSlash = httpMessage.Url.LastIndexOf(ConstHelper.SLASH);

            if (TryParseId(httpMessage.Url, lastSlash + 1, out long id))
            {
                httpMessage.Url = StringHelper.Substring(httpMessage.Url, 0, lastSlash);
                httpMessage.Query.Add(ConstHelper.ID, id.ToString());
            }

            // 报头逐行扫描：跳过请求行与空行，其余行按首个冒号拆键值
            httpMessage.Headers = new Dictionary<string, string>(CountHeaderLines(headerStr), StringComparer.OrdinalIgnoreCase);

            var lineStart = lineEnd + 2;

            while (lineStart < headerStr.Length)
            {
                var currentEnd = headerStr.IndexOf(ConstHelper.ENTER, lineStart, StringComparison.Ordinal);

                if (currentEnd < 0) currentEnd = headerStr.Length;

                if (currentEnd > lineStart) AddHeaderRow(httpMessage.Headers, headerStr, lineStart, currentEnd);

                lineStart = currentEnd + 2;
            }

            //cookies
            if (httpMessage.Headers.TryGetValue(RequestHeaderType.Cookie.GetDescription(), out string cookiesStr))
            {
                httpMessage.Cookies = HttpCookies.Parse(cookiesStr);
            }

            // 解析Content-Length，对所有HTTP方法都处理
            if (httpMessage.Headers.TryGetValue(RequestHeaderType.ContentLength.GetDescription(), out string contentLengthStr))
            {
                if (int.TryParse(contentLengthStr, out int cl))
                {
                    httpMessage.ContentLength = cl;
                }
            }

            //带正文的表单分析：PUT/PATCH 的请求体同样可以是 multipart，按内容类型判断而不是只看 POST
            if (httpMessage.ContentLength > 0)
            {
                //form-data
                if (httpMessage.ContentType != null && httpMessage.ContentType.AsSpan().Contains(ConstHelper.FormData.AsSpan(), StringComparison.InvariantCultureIgnoreCase))
                {
                    httpMessage.IsFormData = true;

                    httpMessage.Boundary = "--" + Regex.Split(httpMessage.ContentType, ConstHelper.SEMICOLON)[1].Replace(ConstHelper.BOUNDARY, "");
                }
            }
            return index + 4;
        }

        /// <summary>
        /// 把绝对形式的 request-target（http://host/path、ws://host/path）归一化为路径，
        /// 以便按普通路由处理（SAEA.WebSocket 的 WSClient 会发送绝对 URI）。
        /// </summary>
        static string NormalizeTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return target;

            // 只有 absolute-form（scheme:// 出现在开头）才归一化。
            // 相对形式必须原样保留：query 里可能带 url=http://... 这类值。
            int schemeLength;

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                schemeLength = 7;
            else if (target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                schemeLength = 8;
            else if (target.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
                schemeLength = 5;
            else if (target.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                schemeLength = 6;
            else
                return target;

            var pathIndex = target.IndexOf('/', schemeLength);

            var queryIndex = target.IndexOf('?', schemeLength);

            // http://host?x=1（甚至在 query 里带 /）这类没有路径的绝对形式：
            // 补一个根路径并保留 query，不能整段丢掉
            if (queryIndex >= 0 && (pathIndex < 0 || queryIndex < pathIndex))
                return "/" + target.Substring(queryIndex);

            return pathIndex < 0 ? "/" : target.Substring(pathIndex);
        }

        /// <summary>
        /// 把字节区段按 ASCII 解码为字符串
        /// </summary>
        /// <param name="span">待解码的字节区段</param>
        /// <returns>解码后的文本</returns>
        static string GetAsciiString(ReadOnlySpan<byte> span)
        {
            var length = span.Length;

            if (length == 0) return string.Empty;

            // 从池里借中转缓冲，避免每次解码都新分配一个与报头等长的字节数组
            var rented = MemoryPoolManager.Rent(length);

            try
            {
                span.CopyTo(rented);

                return Encoding.ASCII.GetString(rented, 0, length);
            }
            finally
            {
                MemoryPoolManager.Return(rented, length);
            }
        }

        /// <summary>
        /// 解析请求行「方法 SP 目标 SP 协议」，按空格分段且保留空段
        /// </summary>
        /// <param name="httpMessage">接收解析结果的消息对象</param>
        /// <param name="text">报头全文</param>
        /// <param name="start">请求行起始下标</param>
        /// <param name="end">请求行结束下标（不含）</param>
        static void ParseRequestLine(HttpMessage httpMessage, string text, int start, int end)
        {
            var firstSpace = IndexOfChar(text, ' ', start, end);

            if (firstSpace < 0)
            {
                httpMessage.Method = text.Substring(start, end - start);

                // 只有一段时原实现读 arr[1] 越界，这里抛同一类型的异常
                throw new IndexOutOfRangeException();
            }

            httpMessage.Method = text.Substring(start, firstSpace - start);

            var secondSpace = IndexOfChar(text, ' ', firstSpace + 1, end);

            if (secondSpace < 0)
            {
                httpMessage.RelativeUrl = NormalizeTarget(text.Substring(firstSpace + 1, end - firstSpace - 1));

                // 只有两段时原实现读 arr[2] 越界，这里抛同一类型的异常
                throw new IndexOutOfRangeException();
            }

            httpMessage.RelativeUrl = NormalizeTarget(text.Substring(firstSpace + 1, secondSpace - firstSpace - 1));

            var thirdSpace = IndexOfChar(text, ' ', secondSpace + 1, end);

            httpMessage.Protocal = thirdSpace < 0
                ? text.Substring(secondSpace + 1, end - secondSpace - 1)
                : text.Substring(secondSpace + 1, thirdSpace - secondSpace - 1);
        }

        /// <summary>
        /// 判断地址最后一段是否为整数，是则输出其值
        /// </summary>
        /// <param name="url">已去掉查询串的地址</param>
        /// <param name="start">最后一段的起始下标</param>
        /// <param name="id">解析出的整数值</param>
        /// <returns>最后一段能解析为整数返回 true</returns>
        static bool TryParseId(string url, int start, out long id)
        {
            id = 0;

            var length = url.Length - start;

            if (length <= 0) return false;

            // 纯数字且不超过 18 位时直接按位取值，不必为这一段分配子串
            var allDigits = length <= 18;

            for (var i = start; i < url.Length; i++)
            {
                var c = url[i];

                if (c < '0' || c > '9')
                {
                    allDigits = false;
                    break;
                }
            }

            if (allDigits)
            {
                long value = 0;

                for (var i = start; i < url.Length; i++)
                {
                    value = value * 10 + (url[i] - '0');
                }

                id = value;

                return true;
            }

            // 数字、正负号与空白之外的字符一定解析失败，直接返回，避免为它分配子串
            for (var i = start; i < url.Length; i++)
            {
                var c = url[i];

                if ((c >= '0' && c <= '9') || c == '+' || c == '-' || c == ' ' || (c >= '\t' && c <= '\r'))
                    continue;

                return false;
            }

            // 带符号、空白或位数过多时交给 long.TryParse，避免手工复刻数字样式规则
            return long.TryParse(url.Substring(start), out id);
        }

        /// <summary>
        /// 在文本区间内查找指定字符
        /// </summary>
        /// <param name="text">待查找文本</param>
        /// <param name="value">目标字符</param>
        /// <param name="start">起始下标</param>
        /// <param name="end">结束下标（不含）</param>
        /// <returns>字符下标，未找到返回 -1</returns>
        static int IndexOfChar(string text, char value, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (text[i] == value) return i;
            }

            return -1;
        }

        /// <summary>
        /// 分析request body
        /// </summary>
        /// <param name="requestData">含报头的完整请求字节</param>
        /// <param name="httpMessage">已解析出报头的消息对象，解析结果回写其中</param>
        /// <returns>报文已收全并解析成功返回 true；数据不足或解析异常返回 false</returns>
        public static bool AnalysisBody(byte[] requestData, HttpMessage httpMessage)
        {
            var contentLen = httpMessage.ContentLength;
            var positon = httpMessage.Position;

            if (requestData.Length < positon + contentLen)
                return false;

            httpMessage.Body = requestData.AsSpan().Slice(positon, contentLen).ToArray();
            return ParseBody(httpMessage);
        }

        /// <summary>
        /// 按 ContentType 解析已填充的请求体，产出 Forms / PostFiles / Json。
        /// 调用方负责先把 <see cref="HttpMessage.Body"/> 填好；解析失败按「无表单」处理，不向调用方抛异常。
        /// </summary>
        /// <param name="httpMessage">请求体已填充的消息对象</param>
        /// <returns>解析完成返回 true；内容类型不参与解析或解析异常返回 false</returns>
        internal static bool ParseBody(HttpMessage httpMessage)
        {
            try
            {
                if (httpMessage.Body == null)
                    return false;

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
                                            // 原实现 Take(len - 2).ToArray() 会让文件数据再复制一遍，这里一次复制到位；
                                            // 长度不足 2 时同样得到空数组，与截空结果一致
                                            var dataLength = filePart.Data.Length - 2;
                                            if (dataLength > 0)
                                            {
                                                var trimmed = new byte[dataLength];
                                                Buffer.BlockCopy(filePart.Data, 0, trimmed, 0, dataLength);
                                                filePart.Data = trimmed;
                                            }
                                            else
                                            {
                                                filePart.Data = Array.Empty<byte>();
                                            }
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
            catch
            {
                return false;
            }
        }
        #region private

        /// <summary>
        /// 解析查询串，键原样保留、值做 URL 解码，重复键后者覆盖
        /// </summary>
        /// <param name="row">查询串文本，不含问号</param>
        /// <returns>查询参数集合，无有效参数时返回 null</returns>
        private static Dictionary<string, string> GetRequestQuerys(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;

            var capacity = 1;

            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] == '&') capacity++;
            }

            Dictionary<string, string> dic = new Dictionary<string, string>(capacity, StringComparer.OrdinalIgnoreCase);

            var start = 0;

            // 原实现按 & 拆分并丢弃空段，这里按同一规则跳过空段
            while (start < row.Length)
            {
                var end = IndexOfChar(row, '&', start, row.Length);

                if (end < 0) end = row.Length;

                if (end > start)
                {
                    var index = IndexOfChar(row, '=', start, end);

                    if (index < 0)
                    {
                        // 无 = 的参数原实现用 Substring(0, -1) 抛 ArgumentOutOfRangeException，保持同一行为
                        row.Substring(0, -1);
                    }

                    var key = row.Substring(start, index - start);

                    var value = string.Empty;

                    if (end > index + 1)
                    {
                        value = HttpUtility.UrlDecode(row.Substring(index + 1, end - index - 1));
                    }

                    dic[key] = value;
                }

                start = end + 1;
            }

            return dic.Count > 0 ? dic : null;
        }

        private static Dictionary<string, string> GetRequestForms(string row)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(row)) return dic;

            if (row.IndexOf(ConstHelper.ENTER) > 0 && row.TrimStart().IndexOf("{") != 0)
            {
                var kvs = row.Split(ConstHelper.ENTER, StringSplitOptions.RemoveEmptyEntries);
                if (kvs == null || kvs.Length <= 0) return dic;
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
                if (kvs == null || kvs.Length <= 0) return null;
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


        /// <summary>
        /// 统计报头文本的行数，用于预置报头字典容量
        /// </summary>
        /// <param name="headerStr">报头全文</param>
        /// <returns>换行符个数，不小于实际报头行数</returns>
        private static int CountHeaderLines(string headerStr)
        {
            var count = 0;

            for (var i = 0; i < headerStr.Length - 1; i++)
            {
                if (headerStr[i] == '\r' && headerStr[i + 1] == '\n') count++;
            }

            return count;
        }

        /// <summary>
        /// 把一行报头写入集合：只在恰好一个冒号时取值并去掉两侧空白，无冒号与多冒号一律记空串
        /// </summary>
        /// <param name="headers">报头集合</param>
        /// <param name="text">报头全文</param>
        /// <param name="start">行起始下标</param>
        /// <param name="end">行结束下标（不含）</param>
        private static void AddHeaderRow(Dictionary<string, string> headers, string text, int start, int end)
        {
            var colonIndex = IndexOfChar(text, ':', start, end);

            if (colonIndex < 0)
            {
                headers[text.Substring(start, end - start)] = string.Empty;

                return;
            }

            var key = text.Substring(start, colonIndex - start);

            if (IndexOfChar(text, ':', colonIndex + 1, end) > -1)
            {
                headers[key] = string.Empty;

                return;
            }

            var valueStart = colonIndex + 1;

            var valueEnd = end;

            while (valueStart < valueEnd && char.IsWhiteSpace(text[valueStart])) valueStart++;

            while (valueEnd > valueStart && char.IsWhiteSpace(text[valueEnd - 1])) valueEnd--;

            headers[key] = text.Substring(valueStart, valueEnd - valueStart);
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