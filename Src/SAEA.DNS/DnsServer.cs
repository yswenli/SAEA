/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS
*类 名 称：DnsClient
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.DNS.Coder;
using SAEA.DNS.Model;
using SAEA.DNS.Protocol;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SAEA.DNS
{
    /// <summary>
    /// DnsServer
    /// </summary>
    public class DnsServer : IDisposable
    {
        private const int DEFAULT_PORT = 53;
        private const int UDP_TIMEOUT = 2000;

        public event EventHandler<RequestedEventArgs> OnRequested;
        public event EventHandler<RespondedEventArgs> OnResponded;
        public event EventHandler<EventArgs> OnListening;
        public event EventHandler<ErroredEventArgs> OnErrored;

        private bool _run = false;
        private bool _disposed = false;
        private IServerSocket _udpServer;
        private IRequestCoder _coder;

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="dnsRecords"></param>
        /// <param name="parentServer"></param>
        public DnsServer(DnsRecords dnsRecords, IPEndPoint parentServer) :
            this(new FallbackRequestCoder(dnsRecords, new UdpRequestCoder(parentServer)))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="dnsRecords"></param>
        /// <param name="parentServer"></param>
        /// <param name="port"></param>
        public DnsServer(DnsRecords dnsRecords, IPAddress parentServer, int port = DEFAULT_PORT) :
            this(dnsRecords, new IPEndPoint(parentServer, port))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="dnsRecords"></param>
        /// <param name="parentServer"></param>
        /// <param name="port"></param>
        public DnsServer(DnsRecords dnsRecords, string parentServer = "119.29.29.29", int port = DEFAULT_PORT) :
            this(dnsRecords, IPAddress.Parse(parentServer), port)
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="parentServer"></param>
        public DnsServer(IPEndPoint parentServer) :
            this(new UdpRequestCoder(parentServer))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="parentServer"></param>
        /// <param name="port"></param>
        public DnsServer(IPAddress parentServer, int port = DEFAULT_PORT) :
            this(new IPEndPoint(parentServer, port))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="parentServer"></param>
        /// <param name="port"></param>
        public DnsServer(string parentServer, int port = DEFAULT_PORT) :
            this(IPAddress.Parse(parentServer), port)
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="coder"></param>
        public DnsServer(IRequestCoder coder)
        {
            this._coder = coder;
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        public void Start(int port = DEFAULT_PORT, IPAddress ip = null)
        {
            Start(new IPEndPoint(ip ?? IPAddress.Any, port));
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public void Start(IPEndPoint endpoint)
        {
            if (!_run)
            {
                _run = true;
                try
                {
                    _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                                               .SetIPEndPoint(endpoint)
                                               .UseIocp<BaseContext>()
                                               .SetReadBufferSize(SocketOption.UDPMaxLength)
                                               .SetWriteBufferSize(SocketOption.UDPMaxLength)
                                               .SetTimeOut(UDP_TIMEOUT)
                                               .Build());

                    _udpServer.OnError += _udpServer_OnError;
                    _udpServer.OnReceive += _udpServer_OnReceive;

                    _udpServer.Start();

                    OnEvent(OnListening, EventArgs.Empty);
                }
                catch (SocketException e)
                {
                    OnError(e);
                    return;
                }
            }
        }

        private void _udpServer_OnReceive(ISession session, byte[] data)
        {
            var ut = (IUserToken)session;

            HandleRequest(ut.ID, data);
        }

        private void _udpServer_OnError(string ID, Exception ex)
        {
            OnError(ex);
        }

        protected virtual void OnEvent<T>(EventHandler<T> handler, T args)
        {
            if (handler != null) handler(this, args);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _run = false;
                    _udpServer?.Dispose();
                }
            }
        }

        private void OnError(Exception e)
        {
            OnEvent(OnErrored, new ErroredEventArgs(e));
        }

        private async void HandleRequest(string sessionID, byte[] data)
        {
            DnsRequestMessage request = null;

            try
            {
                request = DnsRequestMessage.FromArray(data);

                OnEvent(OnRequested, new RequestedEventArgs(request, data));

                IResponse response = await _coder.Code(request);

                OnEvent(OnResponded, new RespondedEventArgs(request, response, data));

                _udpServer.SendAsync(sessionID, response.ToArray());
            }
            catch (SocketException e) { OnError(e); }
            catch (ArgumentException e) { OnError(e); }
            catch (IndexOutOfRangeException e) { OnError(e); }
            catch (OperationCanceledException e) { OnError(e); }
            catch (IOException e) { OnError(e); }
            catch (ObjectDisposedException e) { OnError(e); }
            catch (ResponseException e)
            {
                IResponse response = e.Response;

                if (response == null)
                {
                    response = DnsResponseMessage.FromRequest(request);
                }

                try
                {
                    _udpServer.SendAsync(sessionID, response.ToArray());
                }
                catch (SocketException) { }
                catch (OperationCanceledException) { }
                finally { OnError(e); }
            }
        }


        public void Dispose()
        {
            Dispose(true);
        }
    }
}
