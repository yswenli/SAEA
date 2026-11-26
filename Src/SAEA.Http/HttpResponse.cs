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
*命名空间：SAEA.Http
*文件名： HttpResponse
*版本号： v26.4.23.1
*唯一标识：c4c89246-1aac-4f49-8360-8cb027672604
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：HttpResponse类
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 16:21:35
*修改人： yswenli
*版本号： v26.11.26.1
*描述：Content-Encoding 按实际压缩置位，SendHeader 保留显式 Connection，新增段发送重载，ApplyRangeHeader 全量区间不再复制
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Encryption;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Http
{
    /// <summary>
    /// Http 响应实体，负责组装报头与报文体并下发到连接
    /// </summary>
    public class HttpResponse : HttpBase, IDisposable
    {
        /// <summary>
        /// 响应状态码，默认为 200
        /// </summary>
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        /// <summary>
        /// 是否已升级为 WebSocket
        /// </summary>
        public bool IsUpgraded { get; private set; }

        /// <summary>
        /// 是否已直接写过原始响应（如握手失败的 400/426）
        /// </summary>
        public bool IsHandled { get; private set; }

        /// <summary>
        /// 只发报头不发正文；HEAD 请求置位，Content-Length 仍按等价 GET 的正文长度给出
        /// </summary>
        public bool SuppressBody { get; set; }

        internal IWebHost WebHost { get; set; }

        bool _isZiped = false;

        // gzip 头只在真正压缩了正文的那一帧写出：流式与池化路径从不压缩，声明了就是骗客户端
        bool _gzipApplied = false;

        // 空行与报头行尾用同一组换行字节：每请求都 Encoding.GetBytes 一次纯属重复分配，
        // 数组只读不写，可安全共享
        static readonly byte[] _newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

        // Date 头每秒最多格式化一次：缓存键是本地时间戳的秒数，值与秒成对写入。
        // 多线程下可能重复计算一次，也可能让值与秒错开一代（Date 滞后不超过 1 秒，HTTP 允许）
        static long _dateHeaderSecond = long.MinValue;

        static string _dateHeaderText;

        // Cache-Control 的 Max-Age 文本只随秒数变化；秒数与文本整体替换，
        // 单次读取就能拿到配对的组合，不会读到半新半旧的组合
        static MaxAgeCacheEntry _maxAgeCache;

        // Expires 文本随「当前秒 + 缓存秒数」变化，两者都对上才复用；
        // 每次重算生成一个不可变条目整体替换，避免读到半新半旧的组合
        static ExpiresCacheEntry _expiresCache;

        // 状态行文本只随状态码变化，按码值缓存；数组元素为不可变字符串，并发重复写入同值无副作用
        const int StatusCodeMin = 100;

        const int StatusCodeMax = 511;

        // 报头键名每请求都要用，缓存一次即可，避免枚举装箱与描述查表
        static readonly string _connectionKey = ResponseHeaderType.Connection.GetDescription();

        static readonly string _contentLengthKey = ResponseHeaderType.ContentLength.GetDescription();

        // Content-Length 文本按常见小长度预生成：缓存有界（0–1024），超长正文回退 ToString()，
        // 不会因任意长度无限增长；数值文本不随区域变化，可安全共享
        const int ContentLengthCacheLength = 1025;

        static readonly string[] _contentLengthTexts = CreateContentLengthTexts();

        static readonly string[] _statusTexts = new string[StatusCodeMax - StatusCodeMin + 1];

        /// <summary>
        /// 当前响应关联的连接通道，发送与 WebSocket 升级依赖它
        /// </summary>
        protected IChannel Channel { get; set; }

        internal HttpResponse()
        {

        }

        internal void Init(IWebHost webHost, IChannel channel, string protocal, bool isZiped = false)
        {
            WebHost = webHost;
            Channel = channel;
            Protocal = protocal;
            _isZiped = isZiped;
            _gzipApplied = false;
            // 默认不设置Connection头，根据客户端请求决定
        }

        /// <summary>
        /// 响应报文体内容
        /// </summary>
        public byte[] Body { get; set; }

        internal HttpResponse SetContent(byte[] content)
        {
            this.Body = content;
            this.ContentLength = content?.Length ?? 0;

            return this;
        }

        internal HttpResponse SetContent(string content, Encoding encoding = null)
        {
            //初始化内容
            encoding = encoding != null ? encoding : Encoding.UTF8;
            return SetContent(encoding.GetBytes(content));
        }


        /// <summary>
        /// 读取响应报头
        /// </summary>
        /// <param name="header">响应报头枚举</param>
        /// <returns>报头值，键不存在时抛出 KeyNotFoundException</returns>
        public string GetHeader(ResponseHeaderType header)
        {
            return base.GetHeader(header);
        }


        /// <summary>
        /// 写入响应报头
        /// </summary>
        /// <param name="header">响应报头枚举</param>
        /// <param name="value">报头值</param>
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
            // 只构建报头，不知道正文是否压缩，不能带 Content-Encoding
            _gzipApplied = false;

            // 容量按典型报头一次给够，避免从 16 逐级扩容；结果与逐段扩容完全一致
            var builder = new StringBuilder(512);
            var sink = new StringBuilderHeaderSink(builder);
            WriteHeaderChunks(ref sink);

            return builder.ToString();
        }

        /// <summary>
        /// 按固定顺序逐段写出报头：报头字符串与报头字节两条输出都走这里，只有一份拼接逻辑
        /// </summary>
        /// <param name="sink">报头分段写出目标</param>
        void WriteHeaderChunks<TSink>(ref TSink sink) where TSink : struct, IHttpHeaderChunkSink
        {
            sink.Text(this.Protocal);
            sink.Text(" ");
            sink.Text(GetStatusText(this.Status));
            sink.Break();

            sink.Line(ConstHelper.ServerName);
            sink.Line("X-Powered-By: SAEA.Sockets");

            sink.Text("Date: ");
            sink.Text(GetDateHeaderText());
            sink.Break();

            // 只在本次确实压缩了正文时声明 gzip：计量与写出都读同一个标志，两处不会不一致
            if (_gzipApplied)
                //支持gzip
                sink.Line(ConstHelper.ContentEncoding);

            //支持跨域
            sink.Text(ConstHelper.CrossDomain);

            var headers = this.Headers;
            if (headers.Count > 0)
            {
                foreach (var key in headers.Keys)
                {
                    sink.Text(key);
                    sink.Text(": ");
                    sink.Line(headers[key]);
                }
            }

            var cookies = this.Cookies;
            if (cookies.Count > 0)
            {
                foreach (var cookie in cookies.Values)
                {
                    // Key 为空的 Cookie 整行不输出，与 HttpCookies.ToString 的既有语义一致
                    if (cookie == null || string.IsNullOrEmpty(cookie.Key)) continue;
                    cookie.WriteChunks(ref sink);
                    sink.Break();
                }
            }
        }

        /// <summary>
        /// 取状态行文本，按状态码缓存：报头计量与报头编码各取一次，缓存后每请求不再重复格式化
        /// </summary>
        /// <param name="status">响应状态码</param>
        /// <returns>「状态码 枚举名」形式的文本</returns>
        static string GetStatusText(HttpStatusCode status)
        {
            var code = (int)status;
            if (code < StatusCodeMin || code > StatusCodeMax)
                return status.ToNVString();

            var slot = code - StatusCodeMin;
            var cached = Volatile.Read(ref _statusTexts[slot]);
            if (cached != null)
                return cached;

            var text = status.ToNVString();
            Volatile.Write(ref _statusTexts[slot], text);

            return text;
        }

        /// <summary>
        /// 取 Date 头值，每秒最多格式化一次
        /// </summary>
        /// <returns>GMT 格式的时间字符串</returns>
        static string GetDateHeaderText()
        {
            var now = DateTimeHelper.Now;
            var second = now.Ticks / TimeSpan.TicksPerSecond;

            var cached = Volatile.Read(ref _dateHeaderText);
            if (cached != null && second == Volatile.Read(ref _dateHeaderSecond))
                return cached;

            var text = now.ToGMTString();

            // 先写秒再写值：并发时最坏让值与秒错开一代，Date 最多滞后 1 秒，不会写出非法日期
            Volatile.Write(ref _dateHeaderSecond, second);
            Volatile.Write(ref _dateHeaderText, text);

            return text;
        }

        /// <summary>
        /// 按目标编码取换行字节
        /// </summary>
        /// <param name="encoding">报头编码</param>
        /// <returns>换行字节，UTF-8 复用共享字段</returns>
        static byte[] GetNewLineBytes(Encoding encoding)
        {
            return ReferenceEquals(encoding, Encoding.UTF8) ? _newLineBytes : encoding.GetBytes(Environment.NewLine);
        }

        /// <summary>
        /// 只遍历分段描述统计报头字节数，不产生任何中间字符串
        /// </summary>
        /// <param name="encoding">报头编码</param>
        /// <param name="newLineBytes">换行字节</param>
        /// <returns>报头（含各行行尾）的精确字节数</returns>
        int GetHeaderByteCount(Encoding encoding, byte[] newLineBytes)
        {
            var sink = new HeaderLengthSink();
            sink.Encoding = encoding;
            sink.NewLineLength = newLineBytes.Length;
            WriteHeaderChunks(ref sink);

            return sink.Length;
        }

        /// <summary>
        /// 把报头直接编码进目标缓冲，编码内容与 <see cref="BuildHeader"/> 完全一致
        /// </summary>
        /// <param name="buffer">目标缓冲</param>
        /// <param name="offset">起始写入位置</param>
        /// <param name="encoding">报头编码</param>
        /// <param name="newLineBytes">换行字节</param>
        /// <returns>实际写出的字节数</returns>
        int WriteHeaderBytes(byte[] buffer, int offset, Encoding encoding, byte[] newLineBytes)
        {
            var sink = new HeaderByteSink();
            sink.Encoding = encoding;
            sink.Target = buffer;
            sink.Offset = offset;
            sink.NewLineBytes = newLineBytes;
            WriteHeaderChunks(ref sink);

            return sink.Offset - offset;
        }

        /// <summary>
        /// 生成「报头 + 一个空行」的字节，供 101 升级响应与分段写头使用
        /// </summary>
        /// <param name="encoding">报头编码</param>
        /// <returns>报头与空行组成的字节数组，长度一次性算准</returns>
        byte[] EncodeHeaderWithBlankLine(Encoding encoding)
        {
            // 只写报头的帧没有正文，也就不存在压缩，不能带 Content-Encoding
            _gzipApplied = false;

            var newLineBytes = GetNewLineBytes(encoding);
            var headerLen = GetHeaderByteCount(encoding, newLineBytes);

            var result = new byte[headerLen + newLineBytes.Length];

            var offset = WriteHeaderBytes(result, 0, encoding, newLineBytes);
            Buffer.BlockCopy(newLineBytes, 0, result, offset, newLineBytes.Length);

            return result;
        }

        /// <summary>
        /// 一次 Max-Age 头的计算结果，秒数与文本成对整体替换
        /// </summary>
        private sealed class MaxAgeCacheEntry
        {
            public int Seconds;

            public string Text;
        }

        /// <summary>
        /// 一次 Expires 头的计算结果，秒数与缓存时长都命中时才可复用
        /// </summary>
        private sealed class ExpiresCacheEntry
        {
            public int Second;

            public int CacheSeconds;

            public string Text;
        }

        /// <summary>
        /// 统计报头字节数的写出目标
        /// </summary>
        private struct HeaderLengthSink : IHttpHeaderChunkSink
        {
            public Encoding Encoding;

            public int NewLineLength;

            public int Length;

            public void Text(string value)
            {
                if (!string.IsNullOrEmpty(value))
                    Length += Encoding.GetByteCount(value);
            }

            public void Line(string value)
            {
                Text(value);
                Break();
            }

            public void Break()
            {
                Length += NewLineLength;
            }
        }

        /// <summary>
        /// 把报头直接编码进目标缓冲的写出目标
        /// </summary>
        private struct HeaderByteSink : IHttpHeaderChunkSink
        {
            public Encoding Encoding;

            public byte[] Target;

            public int Offset;

            public byte[] NewLineBytes;

            public void Text(string value)
            {
                if (string.IsNullOrEmpty(value)) return;

                Offset += Encoding.GetBytes(value, 0, value.Length, Target, Offset);
            }

            public void Line(string value)
            {
                Text(value);
                Break();
            }

            public void Break()
            {
                Buffer.BlockCopy(NewLineBytes, 0, Target, Offset, NewLineBytes.Length);
                Offset += NewLineBytes.Length;
            }
        }

        /// <summary>
        /// 设置回复内容
        /// </summary>
        /// <param name="result">业务处理结果，决定状态码、Content-Type 与报文体</param>
        /// <param name="cacheCalcResult">缓存判定参数，逗号分隔两个整数「是否已缓存,缓存秒数」：首项小于 0 表示不缓存并置 no-cache；首项为 0 置 200 并写 Last-Modified；首项为 1 且秒数大于 0 时置 304 并直接返回</param>
        public void SetCached(IHttpResult result, string cacheCalcResult = "-1,-1")
        {
            this.Status = result.Status;

            #region cached

            // 逐段解析后按段数判定，语义与原先 Split(',').Select(Convert.ToInt32).ToArray() 完全一致
            var cacheCount = ParseCacheCalc(cacheCalcResult, out var cacheFlag, out var cacheSeconds);

            if (cacheCount == 2 && cacheFlag >= 0)
            {
                // 报头名与枚举描述的取值逐字一致，直接写字典可省掉枚举装箱与描述查表
                this.Headers["Cache-Control"] = GetMaxAgeText(cacheSeconds);

                this.Headers["Expires"] = GetExpiresText(DateTimeHelper.Now, cacheSeconds);
                this.Status = HttpStatusCode.OK;

                if (cacheFlag == 0)
                {
                    this.Headers["Last-Modified"] = DateTimeHelper.Now.ToGMTString();
                }

                if (cacheFlag == 1)
                {
                    if (cacheSeconds > 0)
                    {
                        this.Status = HttpStatusCode.NotModified;

                        return;
                    }
                }
            }
            else
            {
                this.Headers["Cache-Control"] = "no-cache";
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
        /// 按请求的 Range 头把内存正文裁剪为单区间，或改写为 416 响应
        /// </summary>
        /// <param name="rangeHeader">请求的 Range 头值，可为 null</param>
        public void ApplyRangeHeader(string rangeHeader)
        {
            // gzip 压缩后正文长度与 Content-Length 不一致，按未压缩长度算范围会切错位置
            if (_isZiped) return;

            // 只有 200 完整响应才能改写；304 等状态保持原样
            if (this.Status != HttpStatusCode.OK) return;

            this.Headers[ResponseHeaderType.AcceptRanges.GetDescription()] = "bytes";

            if (string.IsNullOrWhiteSpace(rangeHeader)) return;

            var body = this.Body;

            var total = body == null ? 0 : body.Length;

            var parsed = ByteRangeHeader.Parse(rangeHeader, total);

            if (parsed.Kind == ByteRangeParseKind.Ignore) return;

            if (parsed.Kind == ByteRangeParseKind.Unsatisfiable)
            {
                this.Status = HttpStatusCode.RequestedRangeNotSatisfiable;

                this.Headers[ResponseHeaderType.ContentRange.GetDescription()] =
                    "bytes */" + total.ToString(CultureInfo.InvariantCulture);

                // 显式转型到 byte[]，避免命中 string 重载
                this.SetContent((byte[])null);

                return;
            }

            // 全量区间与完整正文相同：复用原数组，省掉整块复制，Content-Length 已等于 total
            if (parsed.Start == 0 && parsed.Length == total)
            {
                this.Status = HttpStatusCode.PartialContent;

                this.Headers[ResponseHeaderType.ContentRange.GetDescription()] =
                    "bytes 0-" + (total - 1).ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);

                return;
            }

            var slice = new byte[(int)parsed.Length];

            Buffer.BlockCopy(body, (int)parsed.Start, slice, 0, slice.Length);

            this.SetContent(slice);

            this.Status = HttpStatusCode.PartialContent;

            this.Headers[ResponseHeaderType.ContentRange.GetDescription()] =
                "bytes " + parsed.Start.ToString(CultureInfo.InvariantCulture) + "-" + parsed.End.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 取 Cache-Control 的 Max-Age 值，同一秒数只拼接一次
        /// </summary>
        /// <param name="cacheSeconds">缓存秒数</param>
        /// <returns>形如 Max-Age=60 的报头值</returns>
        static string GetMaxAgeText(int cacheSeconds)
        {
            var cached = Volatile.Read(ref _maxAgeCache);

            if (cached != null && cached.Seconds == cacheSeconds) return cached.Text;

            var text = "Max-Age=" + cacheSeconds;

            Volatile.Write(ref _maxAgeCache, new MaxAgeCacheEntry { Seconds = cacheSeconds, Text = text });

            return text;
        }

        /// <summary>
        /// 取 Expires 头值，同一秒内且缓存时长相同时复用上一次的格式化结果
        /// </summary>
        /// <param name="now">本次请求取到的时间，秒数相同的 GMT 文本必然相同</param>
        /// <param name="cacheSeconds">缓存秒数</param>
        /// <returns>过期时刻的 GMT 文本</returns>
        static string GetExpiresText(DateTime now, int cacheSeconds)
        {
            var second = (int)(now.Ticks / TimeSpan.TicksPerSecond);

            var cached = Volatile.Read(ref _expiresCache);

            if (cached != null && cached.Second == second && cached.CacheSeconds == cacheSeconds)
                return cached.Text;

            var text = now.AddSeconds(cacheSeconds).ToGMTString();

            Volatile.Write(ref _expiresCache, new ExpiresCacheEntry { Second = second, CacheSeconds = cacheSeconds, Text = text });

            return text;
        }

        /// <summary>
        /// 按逗号分段解析缓存判定参数，逐段转成整数
        /// </summary>
        /// <param name="cacheCalcResult">逗号分隔的判定参数，如 "-1,-1"</param>
        /// <param name="flag">第一段的值，段数不足时为 0</param>
        /// <param name="seconds">第二段的值，段数不足时为 0</param>
        /// <returns>实际段数，只有 2 段才是有效输入</returns>
        static int ParseCacheCalc(string cacheCalcResult, out int flag, out int seconds)
        {
            flag = 0;
            seconds = 0;

            var count = 0;
            var start = 0;

            // 分段规则与 string.Split(',') 一致：空串算一段、末尾逗号会多出一个空段；
            // 常规写法直接按字符解析不产生子串，带空格、显式正号或数值越界的写法退回 int.Parse，
            // 保证异常类型与数值边界与逐段 int.Parse 完全一致
            while (true)
            {
                var end = cacheCalcResult.IndexOf(',', start);

                var length = (end < 0 ? cacheCalcResult.Length : end) - start;

                var value = TryParseInt(cacheCalcResult, start, length, out var parsed)
                    ? parsed
                    : int.Parse(cacheCalcResult.Substring(start, length));

                if (count == 0)
                    flag = value;
                else if (count == 1)
                    seconds = value;

                count++;

                if (end < 0) break;

                start = end + 1;
            }

            return count;
        }

        /// <summary>
        /// 直接从字符串的指定区间解析十进制整数，不产生子串
        /// </summary>
        /// <param name="text">源字符串</param>
        /// <param name="start">起始位置</param>
        /// <param name="length">区间长度</param>
        /// <param name="value">解析结果</param>
        /// <returns>区间内容是完全由数字组成的十进制整数且在 int 范围内时返回 true；其余情况交由调用方走 int.Parse</returns>
        static bool TryParseInt(string text, int start, int length, out int value)
        {
            value = 0;

            if (length <= 0) return false;

            var index = start;

            var negative = text[index] == '-';

            if (negative || text[index] == '+') index++;

            var digits = start + length - index;

            // 超过 10 位就可能越界，交给 int.Parse 维持原有的 OverflowException 形态
            if (digits <= 0 || digits > 10) return false;

            long result = 0;

            for (; index < start + length; index++)
            {
                var c = text[index];

                if (c < '0' || c > '9') return false;

                result = result * 10 + (c - '0');
            }

            if (negative) result = -result;

            if (result < int.MinValue || result > int.MaxValue) return false;

            value = (int)result;

            return true;
        }

        /// <summary>生成可直接写入传输层的完整响应字节，依次为状态行、报头、空行与报文体。</summary>
        /// <returns>完整响应字节；101 升级响应不覆盖 Connection 与 Content-Length，也不写报文体</returns>
        public byte[] Encode()
        {
            // 协议名缺失时按 HTTP/1.1 兜底，否则状态行会以空格开头，客户端无法解析。
            if (string.IsNullOrEmpty(this.Protocal))
                this.Protocal = "HTTP/1.1";

            if (this.Status == HttpStatusCode.SwitchingProtocols)
            {
                // 101 由业务按握手报头自行组装：此处不能调 ToBytes()，否则会被覆写
                // Connection: close 并追加 Content-Length，破坏升级握手。
                // BuildHeader 已用行尾结束最后一行，这里只补一个空行作为报头结束符。
                return EncodeHeaderWithBlankLine(Encoding.UTF8);
            }

            return ToBytes();
        }

        /// <summary>
        /// 生成数据
        /// </summary>
        /// <returns>完整响应字节，依次为报头、空行、报文体</returns>
        protected byte[] ToBytes()
        {
            var bdata = this.Body;
            var gzipApplied = false;

            if (_isZiped && this.Body != null)
            {
                bdata = GZipHelper.Compress(this.Body);
                gzipApplied = true;
            }

            // 报头里的 Content-Encoding 必须与本次真实压缩结果一致
            _gzipApplied = gzipApplied;

            var bodyLen = 0;

            if (bdata != null)
            {
                bodyLen = bdata.Length;
            }

            PrepareFrameHeaders(bodyLen);

            // 报头、空行与报文体一次成帧：长度先算好只分配一次，报头直接编码进目标帧。
            // 本方法返回的 byte[] 会被调用方长期持有（如 Encode 的返回值），必须精确大小且不能来自池。
            var headerEncoding = Encoding.UTF8;
            var lineLen = _newLineBytes.Length;
            var headerLen = GetHeaderByteCount(headerEncoding, _newLineBytes);

            var result = new byte[headerLen + lineLen + FrameBodyLength(bodyLen)];

            WriteFrame(result, 0, bdata, bodyLen, headerEncoding);

            return result;
        }

        /// <summary>
        /// 尝试把整帧编码进池化缓冲：成功时调用方按「帧 + 归还句柄」移交，写完成或失败后由接收方释放句柄。
        /// gzip 正文与超过中型池阈值的帧仍走一次性分配。
        /// </summary>
        /// <param name="frame">编码后的完整帧，指向池化缓冲的有效段</param>
        /// <param name="owner">背衬缓冲的归还句柄，随帧移交给传输层</param>
        /// <returns>成功编码到池化缓冲返回 true；返回 false 时两个出参均为默认值</returns>
        internal bool TryEncodeToPooled(out ReadOnlyMemory<byte> frame, out IDisposable owner)
        {
            frame = default;
            owner = null;

            // gzip 压缩结果本身就是新数组，池化省不掉这块分配，直接走一次性分配路径
            if (_isZiped && this.Body != null)
                return false;

            // 本路径不压缩正文，任何残留的压缩标志都不能带进本次报头
            _gzipApplied = false;

            var bdata = this.Body;
            var bodyLen = bdata != null ? bdata.Length : 0;

            PrepareFrameHeaders(bodyLen);

            var headerLen = GetHeaderByteCount(Encoding.UTF8, _newLineBytes);
            var totalLen = headerLen + _newLineBytes.Length + FrameBodyLength(bodyLen);

            // 大帧归还成本高、池命中率低，一次性分配更稳，也不让大数组滞留在池里
            if (totalLen > MemoryPoolManager.MediumThreshold)
                return false;

            var lease = MemoryPoolManager.RentPooled(totalLen);

            try
            {
                WriteFrame(lease.Buffer, 0, bdata, bodyLen, Encoding.UTF8);
            }
            catch
            {
                // 计量与写入不一致时不能把租约带出去，先归还再按原路径抛出
                lease.Dispose();
                throw;
            }

            frame = lease.AsMemory();
            owner = lease;

            return true;
        }

        /// <summary>
        /// 预生成 0–1024 的 Content-Length 文本
        /// </summary>
        /// <returns>下标即长度的文本数组</returns>
        static string[] CreateContentLengthTexts()
        {
            var texts = new string[ContentLengthCacheLength];

            for (var i = 0; i < texts.Length; i++)
            {
                texts[i] = i.ToString();
            }

            return texts;
        }

        /// <summary>
        /// 取正文长度的 Content-Length 文本，小长度走缓存，超出缓存范围回退格式化
        /// </summary>
        /// <param name="bodyLen">正文字节长度，非负</param>
        /// <returns>Content-Length 头值</returns>
        static string GetContentLengthText(int bodyLen)
        {
            if ((uint)bodyLen < (uint)ContentLengthCacheLength)
                return _contentLengthTexts[bodyLen];

            return bodyLen.ToString();
        }

        /// <summary>
        /// 补齐 Content-Length 与默认 Connection 头：两者都要进报头，必须在计量之前完成
        /// </summary>
        /// <param name="bodyLen">正文（压缩后）字节长度</param>
        void PrepareFrameHeaders(int bodyLen)
        {
            // 报头键名与 Headers 的比较器都不区分大小写，直接写字典可省掉枚举装箱与描述查表
            this.Headers[_contentLengthKey] = GetContentLengthText(bodyLen);

            // 未显式指定时默认关闭连接：未知调用方仍按短连接处理，保持既有安全性。
            if (!this.Headers.ContainsKey(_connectionKey))
                this.Headers[_connectionKey] = "close";
        }

        /// <summary>
        /// 帧内实际写出的正文字节数：HEAD 只回报头，Content-Length 仍按等价 GET 的长度给出
        /// </summary>
        /// <param name="bodyLen">正文（压缩后）字节长度</param>
        /// <returns>帧里追加的正文长度</returns>
        int FrameBodyLength(int bodyLen)
        {
            return SuppressBody ? 0 : bodyLen;
        }

        /// <summary>
        /// 把「报头 + 空行 + 正文」写入目标缓冲，写入内容与 <see cref="BuildHeader"/> 逐字节一致
        /// </summary>
        /// <param name="buffer">目标缓冲</param>
        /// <param name="offset">起始写入位置</param>
        /// <param name="body">正文字节，可为 null</param>
        /// <param name="bodyLen">正文字节长度</param>
        /// <param name="headerEncoding">报头编码</param>
        void WriteFrame(byte[] buffer, int offset, byte[] body, int bodyLen, Encoding headerEncoding)
        {
            var headerOffset = WriteHeaderBytes(buffer, offset, headerEncoding, _newLineBytes);
            Buffer.BlockCopy(_newLineBytes, 0, buffer, headerOffset, _newLineBytes.Length);

            var sentBodyLen = FrameBodyLength(bodyLen);
            if (sentBodyLen > 0)
                Buffer.BlockCopy(body, 0, buffer, headerOffset + _newLineBytes.Length, sentBodyLen);
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
            SetContent(data);
        }

        #region WebSocket 升级

        /// <summary>
        /// 升级为 WebSocket 连接：先写 101，再切换到原始字节模式
        /// </summary>
        /// <param name="handshake">101 响应原始字节</param>
        /// <param name="onData">升级后收到的原始字节</param>
        /// <param name="onError">onData 抛出的异常</param>
        /// <returns>底层传输通道</returns>
        public IWebSocketTransport Upgrade(byte[] handshake, Action<byte[]> onData, Action<Exception> onError)
        {
            if (IsUpgraded) throw new InvalidOperationException("当前响应已完成 WebSocket 升级");
            if (IsHandled) throw new InvalidOperationException("当前响应已直接写过原始内容");

            var channel = Channel;
            var webHost = WebHost;

            if (channel == null || webHost == null)
                throw new InvalidOperationException("当前上下文没有关联到有效连接，无法升级为 WebSocket");

            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
                throw new NotSupportedException("当前连接没有可用的处理器链，无法升级为 WebSocket");

            // 顺序很重要：必须等 101 真正交接完成再切换分帧器，否则后续帧会越过尚未写出的握手响应。
            if (handshake != null && handshake.Length > 0)
                session.WriteBytes(handshake);

            // 先做会话升级：登记接收入口、切换分帧器并标记已升级。失败时保持 HTTP 状态，
            // 响应也不应留在「已升级」的假象里，避免后续重复升级被误判。
            session.Upgrade(onData, onError);

            IsUpgraded = true;

            return new HttpWebSocketTransport(webHost, session);
        }

        /// <summary>
        /// 直接写原始响应字节（握手失败等场景），绕过 BuildHeader/ToBytes
        /// </summary>
        /// <param name="data">原始字节，报头由调用方组装；close 为 true 时必须已写入 Connection: close</param>
        /// <param name="close">写完后是否断开连接</param>
        /// <returns>是否已写出</returns>
        public bool WriteRaw(byte[] data, bool close)
        {
            return WriteRawAsync(data, close).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步直接写原始响应字节（握手失败等场景），绕过 BuildHeader/ToBytes
        /// </summary>
        /// <param name="data">原始字节，报头由调用方组装；close 为 true 时必须已写入 Connection: close</param>
        /// <param name="close">写完后是否断开连接</param>
        /// <returns>已写出时返回 true；已升级、已直接写过或没有关联连接时返回 false</returns>
        public Task<bool> WriteRawAsync(byte[] data, bool close)
        {
            return WriteRawCoreAsync(data, close);
        }

        private async Task<bool> WriteRawCoreAsync(byte[] data, bool close)
        {
            if (IsUpgraded || IsHandled) return false;

            var channel = Channel;
            var webHost = WebHost;

            if (channel == null || webHost == null) return false;

            if (data != null && data.Length > 0)
                await SendToHostAsync(channel, data).ConfigureAwait(false);

            IsHandled = true;

            if (close)
                webHost.Disconnect(channel);

            return true;
        }

        #endregion

        #region 针对类似大文件、事件流场景分开处理

        /// <summary>
        /// 仅发送头，针对类似大文件、事件流场景分开处理，
        /// 与SendData，SendEnd联合使用
        /// </summary>
        /// <param name="bodyLen"></param>
        /// <param name="encoding"></param>
        public void SendHeader(long bodyLen, Encoding encoding = null)
        {
            SendHeaderAsync(bodyLen, encoding).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步仅发送头，针对类似大文件、事件流场景分开处理，
        /// 与 <see cref="SendDataAsync"/>、<see cref="SendEndAsync"/> 联合使用
        /// </summary>
        /// <param name="bodyLen">报文体长度；小于 0 表示无限长度流，保持连接</param>
        /// <param name="encoding">报头编码，未指定时用 UTF-8</param>
        /// <returns>写出完成后的任务</returns>
        public Task SendHeaderAsync(long bodyLen, Encoding encoding = null)
        {
            return SendHeaderCoreAsync(bodyLen, encoding);
        }

        private async Task SendHeaderCoreAsync(long bodyLen, Encoding encoding)
        {
            if (IsUpgraded || IsHandled) return;

            if (encoding == null) encoding = Encoding.UTF8;

            if (bodyLen > 0)
                this.SetHeader(ResponseHeaderType.ContentLength, bodyLen.ToString());

            // 只有调用方没有显式指定 Connection 时才写默认值：大文件流可预设 close，
            // 而 SSE 这类未预设的无限流仍按 keep-alive 保持连接
            if (!this.Headers.ContainsKey(_connectionKey))
            {
                this.Headers[_connectionKey] = bodyLen >= 0 ? "close" : "keep-alive";
            }

            // 与 ToBytes 同理：先算长度一次分配，报头直接编进目标数组，返回的 byte[] 会被发送流程继续持有
            var arr = EncodeHeaderWithBlankLine(encoding);

            if (Channel != null)
                await SendToHostAsync(Channel, arr).ConfigureAwait(false);
        }


        /// <summary>
        /// 发送数据，
        /// 与SendHeader，SendEnd联合使用
        /// </summary>
        /// <param name="data"></param>
        public void SendData(byte[] data)
        {
            SendDataAsync(data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 发送缓冲中的一段字节，不复制底层缓冲，供大数据流按块下发
        /// </summary>
        /// <param name="data">背衬缓冲</param>
        /// <param name="offset">起始下标</param>
        /// <param name="count">有效字节数</param>
        internal void SendData(byte[] data, int offset, int count)
        {
            SendDataAsync(data, offset, count).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步发送数据，
        /// 与 <see cref="SendHeaderAsync"/>、<see cref="SendEndAsync"/> 联合使用
        /// </summary>
        /// <param name="data">待发送字节</param>
        /// <returns>写出完成后的任务</returns>
        public Task SendDataAsync(byte[] data)
        {
            return SendDataCoreAsync(data);
        }

        /// <summary>
        /// 异步发送缓冲中的一段字节，只下发 offset 起的 count 个字节，不复制底层缓冲
        /// </summary>
        /// <param name="data">背衬缓冲</param>
        /// <param name="offset">起始下标</param>
        /// <param name="count">有效字节数</param>
        /// <returns>写出完成后的任务</returns>
        internal Task SendDataAsync(byte[] data, int offset, int count)
        {
            return SendDataCoreAsync(new ReadOnlyMemory<byte>(data, offset, count));
        }

        private async Task SendDataCoreAsync(ReadOnlyMemory<byte> data)
        {
            if (IsUpgraded || IsHandled) return;

            // HEAD 只回报头：分段写场景（SSE、大文件）在这里统一丢弃正文，避免各调用方重复判断
            if (SuppressBody) return;

            await SendToHostAsync(Channel, data).ConfigureAwait(false);
        }

        /// <summary>
        /// 发送结束，
        /// 与SendHeader，SendData联合使用
        /// </summary>
        public void SendEnd()
        {
            SendEndAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步发送结束，
        /// 与 <see cref="SendHeaderAsync"/>、<see cref="SendDataAsync"/> 联合使用
        /// </summary>
        /// <returns>关闭提交后的任务</returns>
        public Task SendEndAsync()
        {
            return SendEndCoreAsync();
        }

        private Task SendEndCoreAsync()
        {
            if (IsUpgraded || IsHandled) return Task.CompletedTask;

            WebHost.Disconnect(Channel);

            return Task.CompletedTask;
        }

        #endregion


        /// <summary>
        /// 按请求协议与请求的 Connection 头决定本条响应的连接策略，并写入 Connection 响应头。
        /// <para>
        /// HTTP/1.1 及更高版本默认保持连接，只有请求显式声明 close 才关闭；HTTP/1.0 默认关闭，
        /// 只有请求显式声明 keep-alive 才保持连接。调用方据返回值选择 <see cref="Send"/>（保持连接）
        /// 或 <see cref="End"/>（关闭连接），响应头与连接副作用必须一致。
        /// </para>
        /// </summary>
        /// <param name="requestProtocol">请求协议名与版本，如 HTTP/1.0、HTTP/1.1；为空时按 HTTP/1.1 处理</param>
        /// <param name="connectionHeader">请求的 Connection 头值，未携带时传 null 或空字符串</param>
        /// <returns>true 表示保持连接，调用方应走 <see cref="Send"/>；false 表示关闭连接，调用方应走 <see cref="End"/></returns>
        public bool ApplyConnectionPolicy(string requestProtocol, string connectionHeader)
        {
            var keepAlive = AllowsKeepAlive(requestProtocol, connectionHeader);

            // 键名已缓存，直接写字典；走 SetHeader 会为枚举参数装箱，每请求多一个 24 字节的临时对象
            this.Headers[_connectionKey] = keepAlive ? "keep-alive" : "close";

            return keepAlive;
        }

        /// <summary>
        /// 判断本次请求是否允许复用连接。
        /// </summary>
        /// <param name="requestProtocol">请求协议名与版本</param>
        /// <param name="connectionHeader">请求的 Connection 头值</param>
        /// <returns>允许复用连接返回 true</returns>
        private static bool AllowsKeepAlive(string requestProtocol, string connectionHeader)
        {
            var hasKeepAlive = HasConnectionToken(connectionHeader, "keep-alive");
            var hasClose = HasConnectionToken(connectionHeader, "close");

            // HTTP/1.0 默认短连接，只有客户端显式声明 keep-alive 才复用；同时声明 close 时以关闭为准。
            if (IsHttp10(requestProtocol))
                return hasKeepAlive && !hasClose;

            // HTTP/1.1 及更高版本默认长连接，只有客户端显式声明 close 才关闭。
            return !hasClose;
        }

        /// <summary>
        /// 判断协议是否为 HTTP/1.0。
        /// </summary>
        /// <param name="requestProtocol">请求协议名与版本</param>
        /// <returns>HTTP/1.0 返回 true</returns>
        private static bool IsHttp10(string requestProtocol)
        {
            return !string.IsNullOrEmpty(requestProtocol)
                && requestProtocol.Trim().Equals("HTTP/1.0", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断 Connection 头中是否含指定选项。
        /// </summary>
        /// <param name="connectionHeader">请求的 Connection 头值</param>
        /// <param name="token">待匹配的选项名，如 close、keep-alive</param>
        /// <returns>含该选项返回 true</returns>
        private static bool HasConnectionToken(string connectionHeader, string token)
        {
            if (string.IsNullOrEmpty(connectionHeader))
                return false;

            // Connection 头允许逗号分隔多个选项，必须逐项匹配，不能用子串包含判断。
            // 直接在原串上按逗号切片，省掉 Split 生成的分段数组与逐段子串
            var remaining = connectionHeader.AsSpan();

            while (remaining.Length > 0)
            {
                var comma = remaining.IndexOf(',');

                var item = comma < 0 ? remaining : remaining.Slice(0, comma);

                var start = 0;

                var end = item.Length;

                while (start < end && char.IsWhiteSpace(item[start])) start++;

                while (end > start && char.IsWhiteSpace(item[end - 1])) end--;

                if (end - start == token.Length)
                {
                    var matched = true;

                    for (var i = 0; i < token.Length; i++)
                    {
                        if (char.ToUpperInvariant(item[start + i]) != char.ToUpperInvariant(token[i]))
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched) return true;
                }

                if (comma < 0) break;

                remaining = remaining.Slice(comma + 1);
            }

            return false;
        }

        /// <summary>
        /// 发送响应但不关闭连接
        /// </summary>
        public void Send()
        {
            SendAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步发送响应但不关闭连接
        /// </summary>
        /// <returns>写出完成后的任务</returns>
        public Task SendAsync()
        {
            return SendCoreAsync();
        }

        private async Task SendCoreAsync()
        {
            if (IsUpgraded || IsHandled) return;

            var channel = Channel;
            if (channel == null) return;

            var webHost = WebHost as WebHost;

            if (webHost != null)
            {
                // 帧写进池化缓冲后随帧移交，等发送完成即由传输层归还，避免每请求一次整帧分配
                if (TryEncodeToPooled(out var frame, out var owner))
                {
                    await webHost.SendFrameAsync(channel, frame, owner).ConfigureAwait(false);
                    return;
                }

                await webHost.SendAsync(channel, this.ToBytes()).ConfigureAwait(false);
                return;
            }

            WebHost.Send(channel, this.ToBytes());
        }

        /// <summary>
        /// 结束当前流程并关闭连接；编码前强制把 Connection 置为 close，避免响应头与实际连接状态不一致
        /// </summary>
        public void End()
        {
            EndAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步结束当前流程并关闭连接；编码前强制把 Connection 置为 close，写完成后才提交关闭
        /// </summary>
        /// <returns>写出完成并已提交关闭后的任务</returns>
        public Task EndAsync()
        {
            return EndCoreAsync();
        }

        private async Task EndCoreAsync()
        {
            if (IsUpgraded || IsHandled) return;

            var channel = Channel;
            if (channel == null) return;

            this.Headers[_connectionKey] = "close";

            var webHost = WebHost as WebHost;

            if (webHost != null)
            {
                if (TryEncodeToPooled(out var frame, out var owner))
                {
                    await webHost.EndFrameAsync(channel, frame, owner).ConfigureAwait(false);
                    Channel = null;
                    return;
                }

                await webHost.EndAsync(channel, this.ToBytes()).ConfigureAwait(false);
            }
            else
                WebHost.End(channel, this.ToBytes());

            Channel = null;
        }

        /// <summary>
        /// 是否在连
        /// </summary>
        /// <returns></returns>
        public bool IsConnect()
        {
            var channel = Channel;
            if (channel == null) return false;
            return channel.IsActive;
        }



        /// <summary>
        /// 把一段完整字节写到连接：内置宿主走异步写出，外部 IWebHost 实现退回同步写，行为不变
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待写出的字节，可以是池化缓冲中的一段</param>
        /// <returns>写出完成后的任务</returns>
        private async Task SendToHostAsync(IChannel channel, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0)
                return;

            var webHost = WebHost as WebHost;

            if (webHost != null)
            {
                await webHost.SendAsync(channel, data).ConfigureAwait(false);
                return;
            }

            // 外部 IWebHost 只接受独立数组：整段时直接复用背衬，切片时才复制
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Offset == 0 && segment.Count == segment.Array.Length)
                WebHost.Send(channel, segment.Array);
            else
                WebHost.Send(channel, data.ToArray());
        }

        /// <summary>
        /// 复位响应，使同一个实例可以承载下一条请求；复位后的状态与新建实例一致。
        /// </summary>
        internal void ResetForNextRequest()
        {
            this.Status = HttpStatusCode.OK;
            this.IsUpgraded = false;
            this.IsHandled = false;
            this.SuppressBody = false;
            this.WebHost = null;
            this.Channel = null;
            this._isZiped = false;
            this._gzipApplied = false;

            this.Method = null;
            this.Url = null;
            this.RelativeUrl = null;
            this.Protocal = null;
            this.HeaderStr = null;
            this.IsFormData = false;
            this.Boundary = null;
            this.ContentLength = 0;
            this.Body = null;

            // 集合沿用原实例只清空内容：语义等同新建的空集合，且不产生额外分配
            if (this.Query != null) this.Query.Clear();
            if (this.Forms != null) this.Forms.Clear();
            if (this.Parmas != null) this.Parmas.Clear();
            if (this.Headers != null) this.Headers.Clear();
            if (this.Cookies != null) this.Cookies.Clear();
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

            // 复用实例在请求结束后仍存活，必须摘掉正文引用，否则空闲的保活连接会一直钉住上一次的响应正文
            this.Body = null;
        }

    }
}