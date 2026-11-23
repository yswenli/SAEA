/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Http.Tests
*文件名： RequestDataReaderTests
*版本号： net10.0
*唯一标识：45acd859-8d68-40a6-ad71-24bfc95a08da
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 17:07:24
*描述：请求报头解析（RequestDataReader）的解析结果用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 17:07:24
*修改人： yswenli
*版本号： net10.0
*描述：新建用例，锁定报头与查询串改下标扫描后的解析结果；补充请求行前空行与裸 LF 的用例
*
*****************************************************************************/
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Base;

namespace SAEA.Http.Tests
{
    /// <summary>请求报头解析：解析结果逐字段校验。</summary>
    [TestClass]
    public class RequestDataReaderTests
    {
        /// <summary>
        /// 普通 GET 应解析出方法、目标、协议与报头，位置指向报头之后的正文起点。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisSimpleGet_ParsesRequestLineAndHeaders()
        {
            var bytes = Bytes(
                "GET /bench/echo HTTP/1.1\r\n"
                + "Host: example.com\r\n"
                + "Connection: keep-alive\r\n"
                + "User-Agent: bench/1.0\r\n"
                + "\r\n");

            var headerLength = RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(bytes.Length, headerLength, "请求行与报头之后没有正文，返回长度应等于整帧长度。");
            Assert.AreEqual("GET", message.Method, "应解析出请求方法。");
            Assert.AreEqual("/bench/echo", message.RelativeUrl, "应解析出原始目标。");
            Assert.AreEqual("/bench/echo", message.Url, "应解析出去掉查询串的地址。");
            Assert.AreEqual("HTTP/1.1", message.Protocal, "应解析出协议名与版本。");
            Assert.AreEqual(bytes.Length, message.Position, "Position 应指向报头结束处。");
            Assert.IsTrue(message.HeaderStr.EndsWith("\r\n\r\n"), "报头文本应包含结束空行。");
            Assert.AreEqual(3, message.Headers.Count, "应解析出三个报头。");
            Assert.AreEqual("example.com", message.Headers["Host"], "报头值应去掉两侧空白。");
            Assert.AreEqual(0, message.Query.Count, "无查询串时查询集合应为空。");
        }

        /// <summary>
        /// 查询串的重复键取后出现的值，值做 URL 解码，空键与空值照原样保留。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisQueryWithDuplicateKey_LastValueWins()
        {
            var bytes = Bytes(
                "GET /search?id=1&name=abc&id=2&q=%E4%B8%AD&empty=&=v HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual("/search", message.Url, "地址应为问号之前的部分。");
            Assert.AreEqual(5, message.Query.Count, "应解析出五个查询参数。");
            Assert.AreEqual("2", message.Query["id"], "重复键应保留后出现的值。");
            Assert.AreEqual("abc", message.Query["name"], "普通值应原样保留。");
            Assert.AreEqual("中", message.Query["q"], "值应做 URL 解码。");
            Assert.AreEqual(string.Empty, message.Query["empty"], "空值应保留为空串。");
            Assert.AreEqual("v", message.Query[string.Empty], "空前缀的键应原样保留。");
        }

        /// <summary>
        /// 一个 Cookie 报头里用分号分隔的多个 Cookie 应全部解析，值做 URL 与 HTML 解码。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisMultipleCookies_ParsesAllPairs()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: a=1; b=2; c=%E4%B8%AD; bare\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(4, message.Cookies.Count, "分号分隔的 Cookie 应全部解析。");
            Assert.AreEqual("1", message.Cookies["a"].Value, "应解析出 Cookie 值。");
            Assert.AreEqual("2", message.Cookies["b"].Value, "应解析出全部 Cookie。");
            Assert.AreEqual("中", message.Cookies["c"].Value, "Cookie 值应做 URL 解码。");
            Assert.AreEqual(string.Empty, message.Cookies["bare"].Value, "没有等号的 Cookie 值应为空串。");
            Assert.AreEqual("/", message.Cookies["a"].Path, "解析出的 Cookie 路径应为根路径。");
        }

        /// <summary>
        /// 表单 POST 应解析出 Content-Length，并把请求体拆成表单参数。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisFormPost_BuildsFormsAndBody()
        {
            var body = "id=1&name=abcdefghij";
            var bytes = Bytes(
                "POST /bench/echo HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Content-Type: application/x-www-form-urlencoded\r\n"
                + "Content-Length: " + body.Length + "\r\n"
                + "\r\n"
                + body);

            var headerLength = RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(bytes.Length - body.Length, headerLength, "返回长度应停在正文起点。");
            Assert.AreEqual(body.Length, message.ContentLength, "应解析出 Content-Length。");
            Assert.IsTrue(RequestDataReader.AnalysisBody(bytes, message), "正文已收全时应解析成功。");
            Assert.AreEqual(body.Length, message.Body.Length, "正文长度应与 Content-Length 一致。");
            Assert.AreEqual("1", message.Forms["id"], "应解析出表单参数。");
            Assert.AreEqual("abcdefghij", message.Forms["name"], "应解析出全部表单参数。");
        }

        /// <summary>
        /// JSON POST 应把请求体原样解出到 Json。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisJsonPost_SetsJson()
        {
            var body = "{\"n\":1}";
            var bytes = Bytes(
                "POST /api/save HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Content-Type: application/json; charset=UTF-8\r\n"
                + "Content-Length: " + body.Length + "\r\n"
                + "\r\n"
                + body);

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.IsTrue(RequestDataReader.AnalysisBody(bytes, message), "正文已收全时应解析成功。");
            Assert.AreEqual(body, message.Json, "应把请求体解为 JSON 文本。");
            Assert.AreEqual(0, message.Forms.Count, "JSON 请求不应产生表单参数。");
        }

        /// <summary>
        /// 报头名大小写混用时按键名不区分大小写合并，同名报头保留后出现的值。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisMixedCaseHeaderNames_LastValueWins()
        {
            var bytes = Bytes(
                "POST /x HTTP/1.1\r\n"
                + "HOST: h\r\n"
                + "content-TYPE: text/plain\r\n"
                + "content-length: 5\r\n"
                + "Content-Length: 6\r\n"
                + "\r\n"
                + "hello!");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(3, message.Headers.Count, "键名大小写不同但内容相同的报头应合并为一条。");
            Assert.AreEqual("h", message.Headers["Host"], "混写键名应能按键名读取。");
            Assert.AreEqual("text/plain", message.Headers["Content-Type"], "混写键名的值应原样保留。");
            Assert.AreEqual(6, message.ContentLength, "同名报头应取后出现的值。");
        }

        /// <summary>
        /// 超长报头值应完整保留，不能截断。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisLongHeaderValue_KeepsWholeValue()
        {
            var longValue = new string('a', 4096);
            var bytes = Bytes(
                "GET /long HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "X-Long: " + longValue + "\r\n"
                + "\r\n");

            var headerLength = RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(bytes.Length, headerLength, "整帧只有报头，返回长度应等于整帧长度。");
            Assert.AreEqual(4096, message.Headers["X-Long"].Length, "超长报头值不应被截断。");
            Assert.AreEqual(longValue, message.Headers["X-Long"], "超长报头值应逐字符保留。");
            Assert.AreEqual(bytes.Length, message.HeaderStr.Length, "报头文本应完整保留。");
        }

        /// <summary>
        /// 报头值里带冒号时保持现行为：整行冒号不止一个，值记为空串。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisHeaderValueWithColon_KeepsEmptyValue()
        {
            var bytes = Bytes(
                "GET /q HTTP/1.1\r\n"
                + "Host: 127.0.0.1:39654\r\n"
                + "X-Addr: a:b:c\r\n"
                + "X-Empty:\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(3, message.Headers.Count, "三个报头都应记入集合。");
            Assert.AreEqual(string.Empty, message.Headers["Host"], "值里带冒号时报头值记为空串。");
            Assert.AreEqual(string.Empty, message.Headers["X-Addr"], "值里有两个冒号时同样记为空串。");
            Assert.AreEqual(string.Empty, message.Headers["X-Empty"], "冒号后没有内容时记为空串。");
        }

        /// <summary>
        /// 地址最后一段是数字时移到查询串的 id 参数，并从地址中截掉该段。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisNumericLastSegment_MovesIdToQuery()
        {
            var bytes = Bytes(
                "GET /article/123?x=1 HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual("/article/123?x=1", message.RelativeUrl, "原始目标应保持不变。");
            Assert.AreEqual("/article", message.Url, "数字结尾的一段应从地址中截掉。");
            Assert.AreEqual(2, message.Query.Count, "查询集合应包含原参数与 id。");
            Assert.AreEqual("1", message.Query["x"], "原有查询参数应保留。");
            Assert.AreEqual("123", message.Query["id"], "数字结尾的一段应写入 id 参数。");
        }

        /// <summary>
        /// 请求行只有两段时保持原行为：抛索引越界异常，且已解析出的方法与目标仍可见。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisRequestLineMissingProtocol_ThrowsIndexOutOfRange()
        {
            var bytes = Bytes("GET /x\r\nHost: h\r\n\r\n");

            Assert.ThrowsExactly<IndexOutOfRangeException>(
                () => RequestDataReader.Analysis(bytes.AsSpan(), out _),
                "请求行缺少协议段时应抛索引越界异常。");
        }

        /// <summary>
        /// 查询参数没有等号时保持原行为：抛范围越界异常。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisQueryItemWithoutEquals_ThrowsArgumentOutOfRange()
        {
            var bytes = Bytes("GET /search?id=1&flag HTTP/1.1\r\nHost: h\r\n\r\n");

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => RequestDataReader.Analysis(bytes.AsSpan(), out _),
                "查询参数没有等号时应抛范围越界异常。");
        }

        /// <summary>
        /// 请求行前有一个空行时应跳过该空行正常解析，报头全文仍按原样保留。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisLeadingCrlfBeforeRequestLine_ParsesRequestLine()
        {
            var bytes = Bytes(
                "\r\n"
                + "GET /bench/echo HTTP/1.1\r\n"
                + "Host: example.com\r\n"
                + "Connection: keep-alive\r\n"
                + "User-Agent: bench/1.0\r\n"
                + "Accept: */*\r\n"
                + "Accept-Encoding: gzip, deflate\r\n"
                + "Cookie: a=1\r\n"
                + "\r\n");

            var headerLength = RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(bytes.Length, headerLength, "空行不应影响报头结束位置。");
            Assert.AreEqual("GET", message.Method, "请求行前的空行应被跳过，方法不应带上换行符。");
            Assert.AreEqual("/bench/echo", message.RelativeUrl, "应解析出原始目标。");
            Assert.AreEqual("HTTP/1.1", message.Protocal, "应解析出协议名与版本。");
            Assert.AreEqual(6, message.Headers.Count, "六个报头都应解析。");
            Assert.AreEqual("example.com", message.Headers["Host"], "报头值应去掉两侧空白。");
            Assert.IsTrue(message.HeaderStr.StartsWith("\r\n"), "报头全文应保留请求行前的空行，不做裁剪。");
            Assert.AreEqual(1, message.Cookies.Count, "应解析出 Cookie。");
        }

        /// <summary>
        /// 请求行前只有裸 LF 时不跳过：原实现按 CRLF 拆分，LF 会留在请求行开头。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_AnalysisBareLfBeforeRequestLine_KeepsLfInMethod()
        {
            var bytes = Bytes("\nGET /lf HTTP/1.1\r\nHost: h\r\n\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual("\nGET", message.Method, "裸 LF 不应被当成空行跳过。");
            Assert.AreEqual("/lf", message.RelativeUrl, "目标仍应正常解析。");
            Assert.AreEqual("HTTP/1.1", message.Protocal, "协议仍应正常解析。");
            Assert.AreEqual(1, message.Headers.Count, "报头应正常解析。");
        }

        private static byte[] Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }
    }
}