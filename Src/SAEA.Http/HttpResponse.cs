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
*修改时间：2026/11/10 10:55:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：响应改为按连接通道写出，升级时等握手写完再切换分帧器，不再依赖底层令牌与编码器
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Encryption;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

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

        internal IWebHost WebHost { get; set; }

        bool _isZiped = false;

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
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{this.Protocal} {Status.ToNVString()}");
            builder.AppendLine(ConstHelper.ServerName);
            builder.AppendLine("X-Powered-By: SAEA.Sockets");

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
        /// <param name="result">业务处理结果，决定状态码、Content-Type 与报文体</param>
        /// <param name="cacheCalcResult">缓存判定参数，逗号分隔两个整数「是否已缓存,缓存秒数」：首项小于 0 表示不缓存并置 no-cache；首项为 0 置 200 并写 Last-Modified；首项为 1 且秒数大于 0 时置 304 并直接返回</param>
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
                    this.Headers["Last-Modified"] = DateTimeHelper.Now.ToGMTString();
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
                var header = BuildHeader();
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                byte[] lineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

                // BuildHeader 已用 AppendLine 结束最后一行，这里只补一个空行作为报头结束符。
                var result = new byte[headerBytes.Length + lineBytes.Length];
                Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
                Buffer.BlockCopy(lineBytes, 0, result, headerBytes.Length, lineBytes.Length);
                return result;
            }

            return ToBytes();
        }

        /// <summary>
        /// 生成数据
        /// </summary>
        /// <returns>完整响应字节，依次为报头、空行、报文体</returns>
        protected byte[] ToBytes()
        {
            List<byte> reponseDataList = new List<byte>();

            byte[] lineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

            var bdata = this.Body;
            if (_isZiped && this.Body != null)
                bdata = GZipHelper.Compress(this.Body);

            var bodyLen = 0;

            if (bdata != null)
            {
                bodyLen = bdata.Length;
            }

            this.SetHeader(ResponseHeaderType.ContentLength, bodyLen.ToString());
            // 总是设置Connection头为close，不支持keep-alive
            this.SetHeader(ResponseHeaderType.Connection, "close");
            var header = BuildHeader();

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            //发送响应头
            reponseDataList.AddRange(headerBytes);
            //发送空行
            reponseDataList.AddRange(lineBytes);

            //发送内容
            if (bdata != null)
                reponseDataList.AddRange(bdata);
            return reponseDataList.ToArray();
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
        /// <param name="data">原始字节</param>
        /// <param name="close">写完后是否断开连接</param>
        /// <returns>是否已写出</returns>
        public bool WriteRaw(byte[] data, bool close)
        {
            if (IsUpgraded || IsHandled) return false;

            var channel = Channel;
            var webHost = WebHost;

            if (channel == null || webHost == null) return false;

            if (data != null && data.Length > 0)
                webHost.Send(channel, data);

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
            if (IsUpgraded || IsHandled) return;

            if (encoding == null) encoding = Encoding.UTF8;

            List<byte> reponseDataList = new List<byte>();

            byte[] lineBytes = encoding.GetBytes(System.Environment.NewLine);

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

            var header = BuildHeader();

            byte[] headerBytes = encoding.GetBytes(header);

            //发送响应头
            reponseDataList.AddRange(headerBytes);
            //发送空行
            reponseDataList.AddRange(lineBytes);

            var arr = reponseDataList.ToArray();

            reponseDataList.Clear();
            if (Channel != null)
                WebHost.Send(Channel, arr);
        }


        /// <summary>
        /// 发送数据，
        /// 与SendHeader，SendEnd联合使用
        /// </summary>
        /// <param name="data"></param>
        public void SendData(byte[] data)
        {
            if (IsUpgraded || IsHandled) return;

            WebHost.Send(Channel, data);
        }

        /// <summary>
        /// 发送结束，
        /// 与SendHeader，SendData联合使用
        /// </summary>
        public void SendEnd()
        {
            if (IsUpgraded || IsHandled) return;

            WebHost.Disconnect(Channel);
        }

        #endregion


        /// <summary>
        /// 发送响应但不关闭连接
        /// </summary>
        public void Send()
        {
            if (IsUpgraded || IsHandled) return;

            if (Channel == null) return;
            else
            {
                WebHost.Send(Channel, this.ToBytes());
            }
        }

        /// <summary>
        /// 结束当前流程
        /// </summary>
        public void End()
        {
            if (IsUpgraded || IsHandled) return;

            var channel = Channel;
            if (channel == null) return;
            else
            {
                WebHost.End(channel, this.ToBytes());
                Channel = null;
            }
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