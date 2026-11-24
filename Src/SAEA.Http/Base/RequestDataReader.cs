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
*修改时间：2026/11/24 15:59:54
*修改人： yswenli
*版本号： v26.11.24.1
*描述：multipart 改为按 64KB 缓冲单遍流式扫描，JSON 与 urlencoded 直接从流读取文本
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Model;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Http.Base
{
    /// <summary>
    /// http字符串读取 
    /// </summary>
    public static class RequestDataReader
    {
        static readonly byte[] _dEnterBytes = new byte[] { 13, 10, 13, 10 };

        /// <summary>
        /// 单个 multipart 请求允许的最大分段数
        /// </summary>
        private const int MaxParts = 1024;

        /// <summary>
        /// 单个 multipart 分段允许的报头块最大字节数
        /// </summary>
        private const int MaxPartHeaderBytes = 16 * 1024;

        // 报头名每请求都要查表，缓存成静态字段可省掉枚举装箱与并发表查找
        static readonly string _cookieKey = RequestHeaderType.Cookie.GetDescription();

        static readonly string _contentLengthKey = RequestHeaderType.ContentLength.GetDescription();

        // 常见请求方法与协议串缓存成静态实例，避免每条请求都从字节临时解出相同的字符串
        static readonly byte[] _getBytes = new byte[] { (byte)'G', (byte)'E', (byte)'T' };

        static readonly byte[] _postBytes = new byte[] { (byte)'P', (byte)'O', (byte)'S', (byte)'T' };

        static readonly byte[] _putBytes = new byte[] { (byte)'P', (byte)'U', (byte)'T' };

        static readonly byte[] _deleteBytes = new byte[] { (byte)'D', (byte)'E', (byte)'L', (byte)'E', (byte)'T', (byte)'E' };

        static readonly byte[] _headBytes = new byte[] { (byte)'H', (byte)'E', (byte)'A', (byte)'D' };

        static readonly byte[] _optionsBytes = new byte[] { (byte)'O', (byte)'P', (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'S' };

        static readonly byte[] _patchBytes = new byte[] { (byte)'P', (byte)'A', (byte)'T', (byte)'C', (byte)'H' };

        static readonly byte[] _http11Bytes = new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1' };

        static readonly byte[] _http10Bytes = new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'0' };

        /// <summary>
        /// 分析request 头部
        /// </summary>
        /// <param name="bufferSpan">报文缓冲，须含完整报头</param>
        /// <param name="httpMessage">解析出的消息，未找到完整报头时为 null</param>
        /// <returns>报头字节长度，未找到完整报头时返回 0</returns>
        public static int Analysis(Span<byte> bufferSpan, out HttpMessage httpMessage)
        {
            return Analysis(bufferSpan, null, out httpMessage);
        }

        /// <summary>
        /// 分析request 头部并把结果写入指定消息对象，供同一连接复用消息实例
        /// </summary>
        /// <param name="bufferSpan">报文缓冲，须含完整报头</param>
        /// <param name="target">复用的消息对象，为 null 时新建；调用方负责在复用前复位</param>
        /// <param name="httpMessage">解析出的消息，未找到完整报头时为 null</param>
        /// <returns>报头字节长度，未找到完整报头时返回 0</returns>
        internal static int Analysis(Span<byte> bufferSpan, HttpMessage target, out HttpMessage httpMessage)
        {
            httpMessage = null;

            var length = bufferSpan.Length;

            var index = bufferSpan.IndexOf(_dEnterBytes);

            if (index < 4) return 0;

            // 缓冲恰好止于报头结束处时整段都是报头，否则截到分隔串之后
            var header = index == length - 4 ? bufferSpan : bufferSpan.Slice(0, index + 4);

            var message = target ?? new HttpMessage();

            // 解析直接用帧内字节，不再为整段报头建字符串；
            // 报头原文等解析成功后（见方法末尾）再存进池化缓冲，解析中途抛异常时不会有借出的缓冲无人归还
            message.Position = header.Length;

            // 请求行前允许有空行，跳过前导的完整 CRLF 对（只跳成对的，单独的 LF 留在请求行里）
            var requestLineStart = 0;

            while (requestLineStart + 1 < header.Length && header[requestLineStart] == (byte)'\r' && header[requestLineStart + 1] == (byte)'\n')
                requestLineStart += 2;

            var lineEnd = IndexOfLineEnd(header, requestLineStart);

            if (lineEnd < 0) lineEnd = header.Length;

            ParseRequestLine(message, header, requestLineStart, lineEnd);

            var questionIndex = message.RelativeUrl.IndexOf(ConstHelper.QUESTIONMARK);

            if (questionIndex > -1)
            {
                var relativeUrl = message.RelativeUrl;

                message.Url = relativeUrl.Substring(0, questionIndex);

                // 原实现取 Split('?') 的 qarr[1]，即首个与第二个 '?' 之间的部分
                var nextQuestionIndex = relativeUrl.IndexOf(ConstHelper.QUESTIONMARK, questionIndex + 1);

                var queryEnd = nextQuestionIndex < 0 ? relativeUrl.Length : nextQuestionIndex;

                message.Query = GetRequestQuerys(relativeUrl.Substring(questionIndex + 1, queryEnd - questionIndex - 1), target == null ? null : message.Query);
            }
            else
            {
                message.Url = message.RelativeUrl;
            }

            var lastSlash = message.Url.LastIndexOf(ConstHelper.SLASH);

            if (TryParseId(message.Url, lastSlash + 1, out long id))
            {
                message.Url = StringHelper.Substring(message.Url, 0, lastSlash);
                message.Query.Add(ConstHelper.ID, id.ToString());
            }

            // 报头逐行扫描：跳过请求行与空行，其余行按首个冒号拆键值
            var headers = message.Headers;

            if (headers == null)
            {
                message.Headers = headers = new Dictionary<string, string>(CountHeaderLines(header), StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // 复用只清空内容，比较器与容量沿用，语义等同新建的空集合
                headers.Clear();
            }

            var lineStart = lineEnd + 2;

            while (lineStart < header.Length)
            {
                var currentEnd = IndexOfLineEnd(header, lineStart);

                if (currentEnd < 0) currentEnd = header.Length;

                if (currentEnd > lineStart) AddHeaderRow(headers, header, lineStart, currentEnd);

                lineStart = currentEnd + 2;
            }

            //cookies
            // 只留报头原文，首次读取 Cookies 时才解析：不读 Cookie 的请求省掉集合、Cookie 实体与名字子串
            if (headers.TryGetValue(_cookieKey, out string cookiesStr))
            {
                message.SetCookieHeader(cookiesStr);
            }
            else
            {
                // 复用实例必须显式清空，否则会沿用上一次请求的 Cookie 报头
                message.SetCookieHeader(null);
            }

            // 解析Content-Length，对所有HTTP方法都处理
            if (headers.TryGetValue(_contentLengthKey, out string contentLengthStr))
            {
                if (int.TryParse(contentLengthStr, out int cl))
                {
                    message.ContentLength = cl;
                }
            }

            //带正文的表单分析：PUT/PATCH 的请求体同样可以是 multipart，按内容类型判断而不是只看 POST
            if (message.ContentLength > 0)
            {
                //form-data
                if (message.ContentType != null && message.ContentType.AsSpan().Contains(ConstHelper.FormData.AsSpan(), StringComparison.InvariantCultureIgnoreCase))
                {
                    var boundary = GetMultipartBoundary(message.ContentType);

                    // 没有 boundary 参数就无法切分分段，保持 IsFormData 为 false，不进入 multipart 解析
                    if (!string.IsNullOrEmpty(boundary))
                    {
                        message.IsFormData = true;

                        // 对外格式保持 "--" + token，解析器按同一个串定位分段
                        message.Boundary = "--" + boundary;
                    }
                }
            }

            // 解析成功后把报头原文留在池化缓冲里，供 HeaderStr 按需解码
            message.SetHeaderSource(header);

            httpMessage = message;

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
        /// <param name="text">报头字节区段</param>
        /// <param name="start">请求行起始下标</param>
        /// <param name="end">请求行结束下标（不含）</param>
        static void ParseRequestLine(HttpMessage httpMessage, ReadOnlySpan<byte> text, int start, int end)
        {
            var firstSpace = IndexOfByte(text, (byte)' ', start, end);

            if (firstSpace < 0)
            {
                httpMessage.Method = GetAsciiString(text.Slice(start, end - start));

                // 只有一段时原实现读 arr[1] 越界，这里抛同一类型的异常
                throw new IndexOutOfRangeException();
            }

            httpMessage.Method = GetMethodString(text.Slice(start, firstSpace - start));

            var secondSpace = IndexOfByte(text, (byte)' ', firstSpace + 1, end);

            if (secondSpace < 0)
            {
                httpMessage.RelativeUrl = NormalizeTarget(GetAsciiString(text.Slice(firstSpace + 1, end - firstSpace - 1)));

                // 只有两段时原实现读 arr[2] 越界，这里抛同一类型的异常
                throw new IndexOutOfRangeException();
            }

            httpMessage.RelativeUrl = NormalizeTarget(GetAsciiString(text.Slice(firstSpace + 1, secondSpace - firstSpace - 1)));

            var thirdSpace = IndexOfByte(text, (byte)' ', secondSpace + 1, end);

            httpMessage.Protocal = thirdSpace < 0
                ? GetProtocolString(text.Slice(secondSpace + 1, end - secondSpace - 1))
                : GetProtocolString(text.Slice(secondSpace + 1, thirdSpace - secondSpace - 1));
        }

        /// <summary>
        /// 把常见请求方法的字节解成静态字符串，未命中时回退到 ASCII 解码
        /// </summary>
        /// <param name="span">方法名所在字节区段</param>
        /// <returns>方法名文本</returns>
        static string GetMethodString(ReadOnlySpan<byte> span)
        {
            if (span.SequenceEqual(_getBytes)) return "GET";
            if (span.SequenceEqual(_postBytes)) return "POST";
            if (span.SequenceEqual(_putBytes)) return "PUT";
            if (span.SequenceEqual(_deleteBytes)) return "DELETE";
            if (span.SequenceEqual(_headBytes)) return "HEAD";
            if (span.SequenceEqual(_optionsBytes)) return "OPTIONS";
            if (span.SequenceEqual(_patchBytes)) return "PATCH";

            return GetAsciiString(span);
        }

        /// <summary>
        /// 把常见协议串的字节解成静态字符串，未命中时回退到 ASCII 解码
        /// </summary>
        /// <param name="span">协议串所在字节区段</param>
        /// <returns>协议串文本</returns>
        static string GetProtocolString(ReadOnlySpan<byte> span)
        {
            if (span.SequenceEqual(_http11Bytes)) return "HTTP/1.1";
            if (span.SequenceEqual(_http10Bytes)) return "HTTP/1.0";

            return GetAsciiString(span);
        }

        /// <summary>
        /// 在字节区段内查找指定字节
        /// </summary>
        /// <param name="text">待查找区段</param>
        /// <param name="value">目标字节</param>
        /// <param name="start">起始下标</param>
        /// <param name="end">结束下标（不含）</param>
        /// <returns>字节下标，未找到返回 -1</returns>
        static int IndexOfByte(ReadOnlySpan<byte> text, byte value, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (text[i] == value) return i;
            }

            return -1;
        }

        /// <summary>
        /// 查找下一个 CRLF 的起始下标
        /// </summary>
        /// <param name="text">待查找区段</param>
        /// <param name="start">起始下标</param>
        /// <returns>CR 的下标，未找到返回 -1</returns>
        static int IndexOfLineEnd(ReadOnlySpan<byte> text, int start)
        {
            for (var i = start; i < text.Length - 1; i++)
            {
                if (text[i] == (byte)'\r' && text[i + 1] == (byte)'\n') return i;
            }

            return -1;
        }

        /// <summary>
        /// 判断字节是否为 ASCII 空白，与字符层面的 char.IsWhiteSpace 在 ASCII 范围内等价
        /// </summary>
        /// <param name="value">待判断的字节</param>
        /// <returns>是空白返回 true</returns>
        static bool IsAsciiWhiteSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\n' || value == (byte)'\v' || value == (byte)'\f' || value == (byte)'\r';
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

            // 只是把传入字节包成只读内存流，不复制；解析结束会把位置复位到 0
            httpMessage.Body = new MemoryStream(requestData, positon, contentLen, false, true);
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
            if (httpMessage == null) return false;

            var body = httpMessage.Body;

            if (body == null || body.Length == 0) return false;

            try
            {
                var contentType = httpMessage.ContentType;

                if (contentType == null) return false;

                if (contentType.IndexOf(ConstHelper.FormUrlEncode, StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    httpMessage.Forms = GetRequestForms(ReadBodyText(body));
                }
                else if (contentType.IndexOf(ConstHelper.FormData, StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    SeekBodyStart(body);

                    return MultipartStreamParser.Parse(body, httpMessage);
                }
                else if (contentType.IndexOf(ConstHelper.Json, StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    httpMessage.Json = ReadBodyText(body);
                }

                return true;
            }
            catch (Exception ex)
            {
                // 畸形请求体或临时文件创建失败都只记日志，按「无表单」处理，不向连接层抛
                LogHelper.Error("RequestDataReader.ParseBody", ex);

                return false;
            }
            finally
            {
                // 解析结束后把位置复位，业务可以从头再读一遍
                ResetBodyPosition(body);
            }
        }

        /// <summary>
        /// 直接从请求体流按 UTF-8 读成字符串，避免先复制整段字节再解码的二次复制
        /// </summary>
        /// <param name="body">请求体流</param>
        /// <returns>请求体文本</returns>
        private static string ReadBodyText(Stream body)
        {
            SeekBodyStart(body);

            // leaveOpen 为 true：流归消息对象所有，读完不能关
            using (var reader = new StreamReader(body, Encoding.UTF8, true, 1024, true))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// 把请求体流定位到开头，不可 Seek 时保持当前进度
        /// </summary>
        /// <param name="body">请求体流</param>
        private static void SeekBodyStart(Stream body)
        {
            if (body.CanSeek) body.Position = 0;
        }

        /// <summary>
        /// 把请求体流的位置复位到 0，已释放或不可 Seek 时忽略
        /// </summary>
        /// <param name="body">请求体流</param>
        private static void ResetBodyPosition(Stream body)
        {
            try
            {
                SeekBodyStart(body);
            }
            catch (Exception)
            {
                // 流已释放或不允许调整位置时保持原状，不影响解析结果
            }
        }
        #region private

        /// <summary>
        /// 解析查询串，键原样保留、值做 URL 解码，重复键后者覆盖
        /// </summary>
        /// <param name="row">查询串文本，不含问号</param>
        /// <param name="reuse">复用的集合，为 null 时新建</param>
        /// <returns>查询参数集合，无有效参数时返回 null</returns>
        private static Dictionary<string, string> GetRequestQuerys(string row, Dictionary<string, string> reuse)
        {
            if (reuse != null) reuse.Clear();

            if (string.IsNullOrEmpty(row)) return null;

            var capacity = 1;

            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] == '&') capacity++;
            }

            Dictionary<string, string> dic = reuse ?? new Dictionary<string, string>(capacity, StringComparer.OrdinalIgnoreCase);

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
        /// 统计报头字节区段的行数，用于预置报头字典容量
        /// </summary>
        /// <param name="header">报头区段</param>
        /// <returns>换行符个数，不小于实际报头行数</returns>
        private static int CountHeaderLines(ReadOnlySpan<byte> header)
        {
            var count = 0;

            for (var i = 0; i < header.Length - 1; i++)
            {
                if (header[i] == (byte)'\r' && header[i + 1] == (byte)'\n') count++;
            }

            return count;
        }

        /// <summary>
        /// 把一行报头写入集合：只在恰好一个冒号时取值并去掉两侧空白，无冒号与多冒号一律记空串
        /// </summary>
        /// <param name="headers">报头集合</param>
        /// <param name="text">报头字节区段</param>
        /// <param name="start">行起始下标</param>
        /// <param name="end">行结束下标（不含）</param>
        private static void AddHeaderRow(Dictionary<string, string> headers, ReadOnlySpan<byte> text, int start, int end)
        {
            var colonIndex = IndexOfByte(text, (byte)':', start, end);

            if (colonIndex < 0)
            {
                headers[GetAsciiString(text.Slice(start, end - start))] = string.Empty;

                return;
            }

            var key = GetAsciiString(text.Slice(start, colonIndex - start));

            if (IndexOfByte(text, (byte)':', colonIndex + 1, end) > -1)
            {
                headers[key] = string.Empty;

                return;
            }

            var valueStart = colonIndex + 1;

            var valueEnd = end;

            while (valueStart < valueEnd && IsAsciiWhiteSpace(text[valueStart])) valueStart++;

            while (valueEnd > valueStart && IsAsciiWhiteSpace(text[valueEnd - 1])) valueEnd--;

            headers[key] = GetAsciiString(text.Slice(valueStart, valueEnd - valueStart));
        }

        /// <summary>
        /// 解析单个分段的报头块，读出字段名、文件名与 MIME 类型
        /// </summary>
        /// <param name="body">含报头块的缓冲区</param>
        /// <param name="start">报头块起始下标</param>
        /// <param name="length">报头块长度，不含结束空行</param>
        /// <param name="name">表单字段名，没有 Content-Disposition 或其中没有 name 时为 null</param>
        /// <param name="fileName">客户端提交的文件名，普通表单字段为 null</param>
        /// <param name="type">MIME 类型，缺省为空串</param>
        /// <returns>报头块里有带 name 的 Content-Disposition 返回 true</returns>
        internal static bool TryParsePartHeaders(ReadOnlySpan<byte> body, int start, int length, out string name, out string fileName, out string type)
        {
            name = null;
            fileName = null;
            type = string.Empty;

            if (length <= 0) return false;

            // 报头块通常只有几百字节，按 UTF-8 一次解出，文件名里的非 ASCII 字符不会被弄坏
            var text = Encoding.UTF8.GetString(body.Slice(start, length).ToArray());

            var block = text.AsSpan();

            var position = 0;

            while (position <= block.Length)
            {
                var relative = block.Slice(position).IndexOf('\n');

                var end = relative < 0 ? block.Length : position + relative;

                var line = block.Slice(position, end - position);

                if (line.Length > 0 && line[line.Length - 1] == '\r') line = line.Slice(0, line.Length - 1);

                var colon = line.IndexOf(':');

                if (colon > 0)
                {
                    var headerName = TrimWhiteSpace(line.Slice(0, colon));

                    var headerValue = TrimWhiteSpace(line.Slice(colon + 1));

                    if (headerName.Equals("Content-Disposition".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        TryGetParameter(headerValue, "name".AsSpan(), out name);

                        TryGetParameter(headerValue, "filename".AsSpan(), out fileName);
                    }
                    else if (headerName.Equals(ConstHelper.CT.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        // 只取 MIME 类型本身，分段里的 charset 等参数不属于 Type
                        var semicolon = headerValue.IndexOf(';');

                        type = TrimWhiteSpace(semicolon < 0 ? headerValue : headerValue.Slice(0, semicolon)).ToString();
                    }
                }

                if (relative < 0) break;

                position = end + 1;
            }

            return !string.IsNullOrEmpty(name);
        }

        /// <summary>
        /// 在报头值里按分号拆分参数并读取指定键的值，值两侧的引号会去掉
        /// </summary>
        /// <param name="value">报头值</param>
        /// <param name="key">参数名，不区分大小写</param>
        /// <param name="result">参数值，未找到时为 null</param>
        /// <returns>存在该参数返回 true</returns>
        private static bool TryGetParameter(ReadOnlySpan<char> value, ReadOnlySpan<char> key, out string result)
        {
            result = null;

            var position = 0;

            while (position <= value.Length)
            {
                var end = position;

                var quoted = false;

                while (end < value.Length)
                {
                    var current = value[end];

                    if (current == '"')
                    {
                        quoted = !quoted;
                    }
                    else if (current == ';' && !quoted)
                    {
                        break;
                    }

                    end++;
                }

                var segment = TrimWhiteSpace(value.Slice(position, end - position));

                var equals = segment.IndexOf('=');

                if (equals > 0 && TrimWhiteSpace(segment.Slice(0, equals)).Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    var raw = TrimWhiteSpace(segment.Slice(equals + 1));

                    if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                    {
                        raw = raw.Slice(1, raw.Length - 2);
                    }

                    result = raw.ToString();

                    return true;
                }

                if (end >= value.Length) break;

                position = end + 1;
            }

            return false;
        }

        /// <summary>
        /// 去掉字符区段两侧的空白
        /// </summary>
        /// <param name="value">待裁剪的区段</param>
        /// <returns>去掉两侧空白后的区段</returns>
        private static ReadOnlySpan<char> TrimWhiteSpace(ReadOnlySpan<char> value)
        {
            var start = 0;

            var end = value.Length;

            while (start < end && char.IsWhiteSpace(value[start])) start++;

            while (end > start && char.IsWhiteSpace(value[end - 1])) end--;

            return value.Slice(start, end - start);
        }

        /// <summary>
        /// 从 Content-Type 报头值里取 multipart 的 boundary 参数
        /// </summary>
        /// <param name="contentType">请求的 Content-Type 报头值</param>
        /// <returns>去掉两侧引号的 boundary 值；没有该参数或值为空时返回 null</returns>
        private static string GetMultipartBoundary(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return null;

            // 参数可以出现在任意位置，也可以带引号，按分号拆分后按键名查找即可
            if (!TryGetParameter(contentType.AsSpan(), "boundary".AsSpan(), out var boundary)) return null;

            return string.IsNullOrEmpty(boundary) ? null : boundary;
        }
        #endregion
    }
}