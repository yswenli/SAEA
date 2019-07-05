/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：Http2Client
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 16:09:13
*描述：
*=====================================================================
*修改时间：2019/6/28 16:09:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Http2.Core;
using SAEA.Http2.Extentions;
using SAEA.Http2.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Http2.Net
{
    /// <summary>
    /// Http2Client
    /// </summary>
    public class Http2Client
    {
        IClientSocket _clientSocket;

        Connection _conn;

        string _path = string.Empty;

        string _authority = string.Empty;

        /// <summary>
        /// Http2Client
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="useHttp1Upgrade"></param>
        /// <param name="size"></param>
        /// <param name="timeOut"></param>
        public Http2Client(Uri uri, bool useHttp1Upgrade = false, int size = 1024, int timeOut = 180 * 1000) : this(uri.Host, uri.Port, uri.Scheme.ToLowerInvariant(), uri.PathAndQuery, uri.Host + ":" + uri.Port, useHttp1Upgrade)
        {
            if (uri.Scheme.ToLowerInvariant() != "http")
            {
                throw new InvalidOperationException("不支持的协议");
            }
        }

        /// <summary>
        /// Http2Client
        /// </summary>
        /// <param name="host"></param>
        /// <param name="ip"></param>
        /// <param name="scheme"></param>
        /// <param name="path"></param>
        /// <param name="authority"></param>
        /// <param name="useHttp1Upgrade"></param>
        /// <param name="size"></param>
        /// <param name="timeOut"></param>
        public Http2Client(string host, int ip, string scheme, string path, string authority, bool useHttp1Upgrade = false, int size = 1024, int timeOut = 180 * 1000)
        {
            _path = path;

            _authority = authority;

            var options = SocketOptionBuilder.Instance.SetSocket(Sockets.Model.SAEASocketType.Tcp)
                .UseStream()
                .SetIP(host)
                .SetPort(ip)
                .SetReadBufferSize(size)
                .SetWriteBufferSize(size)
                .SetTimeOut(timeOut)
                .Build();

            _clientSocket = SocketFactory.CreateClientSocket(options);

            _clientSocket.ConnectAsync().GetAwaiter();

            var config = new ConnectionConfigurationBuilder(false)
                        .UseSettings(Settings.Default)
                        .UseHuffmanStrategy(HuffmanStrategy.IfSmaller)
                        .Build();

            var wrappedStreams = _clientSocket.Socket.CreateStreams();

            if (useHttp1Upgrade)
            {
                var upgrade = new ClientUpgradeRequestBuilder()
                            .SetHttp2Settings(config.Settings)
                            .Build();

                var upgradeReadStream = new UpgradeReadStream(wrappedStreams.ReadableStream);

                var needExplicitStreamClose = true;

                try
                {

                    var upgradeHeader = "OPTIONS / HTTP/1.1\r\n" +
                                    "Host: " + host + "\r\n" +
                                    "Connection: Upgrade, HTTP2-Settings\r\n" +
                                    "Upgrade: h2c\r\n" +
                                    "HTTP2-Settings: " + upgrade.Base64EncodedSettings + "\r\n\r\n";

                    var encodedHeader = Encoding.ASCII.GetBytes(upgradeHeader);

                    wrappedStreams.WriteableStream.WriteAsync(new ArraySegment<byte>(encodedHeader)).GetAwaiter();

                    upgradeReadStream.WaitForHttpHeader().GetAwaiter();

                    var headerBytes = upgradeReadStream.HeaderBytes;

                    upgradeReadStream.ConsumeHttpHeader();

                    var response = Http1Response.ParseFrom(Encoding.ASCII.GetString(headerBytes.Array, headerBytes.Offset, headerBytes.Count - 4));

                    if (response.StatusCode != "101")
                        throw new Exception("升级失败");

                    if (!response.Headers.Any(hf => hf.Key == "connection" && hf.Value == "Upgrade") ||
                        !response.Headers.Any(hf => hf.Key == "upgrade" && hf.Value == "h2c"))
                        throw new Exception("升级失败");

                    needExplicitStreamClose = false;

                    var conn = new Connection(config, upgradeReadStream, wrappedStreams.WriteableStream, options: new Connection.Options
                    {
                        ClientUpgradeRequest = upgrade,
                    });


                    var upgradeStream = upgrade.UpgradeRequestStream.GetAwaiter().GetResult();

                    upgradeStream.Cancel();

                    _conn = conn;
                }
                finally
                {
                    if (needExplicitStreamClose)
                    {
                        wrappedStreams.WriteableStream.CloseAsync();
                    }
                }
            }
            else
            {
                _conn = new Connection(config, wrappedStreams.ReadableStream, wrappedStreams.WriteableStream, options: new Connection.Options());
            }
        }


        /// <summary>
        /// get
        /// </summary>
        /// <returns></returns>
        public async Task<Http2Result> Get()
        {
            var result = new Http2Result();

            var headers = new HeaderField[]
            {
                new HeaderField { Name = ":method", Value = "GET" },
                new HeaderField { Name = ":scheme", Value = "http" },
                new HeaderField { Name = ":path", Value = _path },
                new HeaderField { Name = ":authority", Value = _authority },
            };

            var stream = await _conn.CreateStreamAsync(headers, true);

            result.Heads = (await stream.ReadHeadersAsync()).ToList();

            List<byte> list = new List<byte>();
            while (true)
            {
                var buf = new byte[8192];
                var res = await stream.ReadAsync(new ArraySegment<byte>(buf));
                if (res.EndOfStream) break;
                list.AddRange(buf.AsSpan().Slice(0, res.BytesRead).ToArray());
            }
            if (list.Any())
            {
                result.Body = list.ToArray();
            }
            return result;
        }

        public async Task<Http2Result> Post(string kvs)
        {
            return await Post(Encoding.UTF8.GetBytes(kvs));
        }

        public async Task<Http2Result> Post(byte[] data)
        {
            var result = new Http2Result();

            var headers = new HeaderField[]
            {
                new HeaderField { Name = ":method", Value = "POST" },
                new HeaderField { Name = ":scheme", Value = "http" },
                new HeaderField { Name = ":path", Value = _path },
                new HeaderField { Name = ":authority", Value = _authority },
            };

            var stream = await _conn.CreateStreamAsync(headers, true);

            result.Heads = (await stream.ReadHeadersAsync()).ToList();

            List<byte> list = new List<byte>();
            while (true)
            {
                var buf = new byte[8192];
                var res = await stream.ReadAsync(new ArraySegment<byte>(buf));
                if (res.EndOfStream) break;
                list.AddRange(buf.AsSpan().Slice(0, res.BytesRead).ToArray());
            }
            if (list.Any())
            {
                result.Body = list.ToArray();
            }
            return result;
        }



    }
}
