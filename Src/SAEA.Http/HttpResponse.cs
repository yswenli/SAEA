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
*修改时间：2026/11/19 17:02:06
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增 SuppressBody：ToBytes 与 SendData 在置位时只发报头，Content-Length 仍按正文长度给出
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Encryption;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Net;
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

        // 空行与报头行尾用同一组换行字节：每请求都 Encoding.GetBytes 一次纯属重复分配，
        // 数组只读不写，可安全共享
        static readonly byte[] _newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

        // Date 头每秒最多格式化一次：缓存键是本地时间戳的秒数，值与秒成对写入。
        // 多线程下可能重复计算一次，也可能让值与秒错开一代（Date 滞后不超过 1 秒，HTTP 允许）
        static long _dateHeaderSecond = long.MinValue;

        static string _dateHeaderText;

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
            // 默认不设置Connection头，根据客户端请求决定
        }

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
            sink.Text(Status.ToNVString());
            sink.Break();

            sink.Line(ConstHelper.ServerName);
            sink.Line("X-Powered-By: SAEA.Sockets");

            sink.Text("Date: ");
            sink.Text(GetDateHeaderText());
            sink.Break();

            if (_isZiped)
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
            var newLineBytes = GetNewLineBytes(encoding);
            var headerLen = GetHeaderByteCount(encoding, newLineBytes);

            var result = new byte[headerLen + newLineBytes.Length];

            var offset = WriteHeaderBytes(result, 0, encoding, newLineBytes);
            Buffer.BlockCopy(newLineBytes, 0, result, offset, newLineBytes.Length);

            return result;
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
                this.SetHeader(ResponseHeaderType.CacheControl, "Max-Age=" + cacheSeconds);
                this.SetHeader(ResponseHeaderType.Expires, DateTimeHelper.Now.AddSeconds(cacheSeconds).ToGMTString());
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
                this.SetHeader(ResponseHeaderType.CacheControl, "no-cache");
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
            // 逐段改用 int.Parse，对前后空格、正负号、非数字与数值越界的处理与 Convert.ToInt32(string) 相同
            while (true)
            {
                var end = cacheCalcResult.IndexOf(',', start);

                var segment = end < 0 ? cacheCalcResult.Substring(start) : cacheCalcResult.Substring(start, end - start);

                var value = int.Parse(segment);

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
            if (_isZiped && this.Body != null)
                bdata = GZipHelper.Compress(this.Body);

            var bodyLen = 0;

            if (bdata != null)
            {
                bodyLen = bdata.Length;
            }

            this.SetHeader(ResponseHeaderType.ContentLength, bodyLen.ToString());

            // 未显式指定时默认关闭连接：未知调用方仍按短连接处理，保持既有安全性。
            // 键名与 Headers 的比较器都不区分大小写，先判存在再写，避免与调用方设置的值重复。
            var connectionKey = ResponseHeaderType.Connection.GetDescription();
            if (!this.Headers.ContainsKey(connectionKey))
                this.SetHeader(ResponseHeaderType.Connection, "close");

            // 报头、空行与报文体一次成帧：长度先算好只分配一次，报头直接编码进目标帧。
            // 返回的 byte[] 会被调用方继续持有（输出缓存、异步写入），因此不能从 ArrayPool 借出。
            var headerEncoding = Encoding.UTF8;
            var lineLen = _newLineBytes.Length;
            var headerLen = GetHeaderByteCount(headerEncoding, _newLineBytes);

            // HEAD 只回报头：Content-Length 仍按等价 GET 的正文长度给出，帧里不追加正文
            var sentBodyLen = SuppressBody ? 0 : bodyLen;

            var result = new byte[headerLen + lineLen + sentBodyLen];

            var headerOffset = WriteHeaderBytes(result, 0, headerEncoding, _newLineBytes);
            Buffer.BlockCopy(_newLineBytes, 0, result, headerOffset, lineLen);

            if (sentBodyLen > 0)
                Buffer.BlockCopy(bdata, 0, result, headerOffset + lineLen, sentBodyLen);

            return result;
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

            // 默认情况下，对普通短连接响应使用 Connection: close
            // 对于长连接/流式场景（如 SSE），调用方可以传入 bodyLen = -1 来表示无限长度流，此时保持连接
            if (bodyLen >= 0)
            {
                Headers["Connection"] = "close";
            }
            else
            {
                // 长连接/流式响应，使用 keep-alive，允许持续发送数据
                Headers["Connection"] = "keep-alive";
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
        /// 异步发送数据，
        /// 与 <see cref="SendHeaderAsync"/>、<see cref="SendEndAsync"/> 联合使用
        /// </summary>
        /// <param name="data">待发送字节</param>
        /// <returns>写出完成后的任务</returns>
        public Task SendDataAsync(byte[] data)
        {
            return SendDataCoreAsync(data);
        }

        private async Task SendDataCoreAsync(byte[] data)
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

            this.SetHeader(ResponseHeaderType.Connection, keepAlive ? "keep-alive" : "close");

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
            var tokens = connectionHeader.Split(',');

            foreach (var item in tokens)
            {
                if (item.Trim().Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;
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

            await SendToHostAsync(channel, this.ToBytes()).ConfigureAwait(false);
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

            this.SetHeader(ResponseHeaderType.Connection, "close");

            var data = this.ToBytes();

            var webHost = WebHost as WebHost;

            if (webHost != null)
                await webHost.EndAsync(channel, data).ConfigureAwait(false);
            else
                WebHost.End(channel, data);

            Channel = null;
        }

        /// <summary>
        /// 把一段完整字节写到连接：内置宿主走异步写出，外部 IWebHost 实现退回同步写，行为不变
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待写出的字节</param>
        /// <returns>写出完成后的任务</returns>
        private async Task SendToHostAsync(IChannel channel, byte[] data)
        {
            var webHost = WebHost as WebHost;

            if (webHost != null)
            {
                await webHost.SendAsync(channel, data).ConfigureAwait(false);
                return;
            }

            WebHost.Send(channel, data);
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
            // Body是byte[]类型，由GC自动管理，不需要手动Clear
        }

    }
}