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
using SAEA.DNS.Common.Utils;
using SAEA.DNS.Model;
using SAEA.DNS.Protocol;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ResponseException = SAEA.DNS.Model.ResponseException;

namespace SAEA.DNS
{
    /// <summary>
    /// DnsServer
    /// </summary>
    public class DnsServer : IDisposable
    {
        private const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
        private const int DEFAULT_PORT = 53;
        private const int UDP_TIMEOUT = 2000;

        public event EventHandler<RequestedEventArgs> OnRequested;
        public event EventHandler<RespondedEventArgs> OnResponded;
        public event EventHandler<EventArgs> OnListening;
        public event EventHandler<ErroredEventArgs> OnErrored;

        private bool _run = true;
        private bool _disposed = false;
        private UdpClient _udp;
        private IRequestCoder _coder;

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="dnsRecords"></param>
        /// <param name="endServer"></param>
        public DnsServer(DnsRecords dnsRecords, IPEndPoint endServer) :
            this(new FallbackRequestCoder(dnsRecords, new UdpRequestCoder(endServer)))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="dnsRecords"></param>
        /// <param name="endServer"></param>
        /// <param name="port"></param>
        public DnsServer(DnsRecords dnsRecords, IPAddress endServer, int port = DEFAULT_PORT) :
            this(dnsRecords, new IPEndPoint(endServer, port))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="dnsRecords"></param>
        /// <param name="endServer"></param>
        /// <param name="port"></param>
        public DnsServer(DnsRecords dnsRecords, string endServer = "119.29.29.29", int port = DEFAULT_PORT) :
            this(dnsRecords, IPAddress.Parse(endServer), port)
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="endServer"></param>
        public DnsServer(IPEndPoint endServer) :
            this(new UdpRequestCoder(endServer))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="endServer"></param>
        /// <param name="port"></param>
        public DnsServer(IPAddress endServer, int port = DEFAULT_PORT) :
            this(new IPEndPoint(endServer, port))
        { }

        /// <summary>
        /// DnsServer
        /// </summary>
        /// <param name="endServer"></param>
        /// <param name="port"></param>
        public DnsServer(string endServer, int port = DEFAULT_PORT) :
            this(IPAddress.Parse(endServer), port)
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
        public Task Start(int port = DEFAULT_PORT, IPAddress ip = null)
        {
            return Start(new IPEndPoint(ip ?? IPAddress.Any, port));
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public async Task Start(IPEndPoint endpoint)
        {
            await Task.Yield();

            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

            if (_run)
            {
                try
                {
                    _udp = new UdpClient(endpoint);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        _udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
                    }
                }
                catch (SocketException e)
                {
                    OnError(e);
                    return;
                }
            }

            AsyncCallback receiveCallback = null;

            receiveCallback = result =>
            {
                byte[] data;

                try
                {
                    IPEndPoint remote = new IPEndPoint(0, 0);
                    data = _udp.EndReceive(result, ref remote);
                    HandleRequest(data, remote);
                }
                catch (ObjectDisposedException)
                {
                    _run = false;
                }
                catch (SocketException e)
                {
                    OnError(e);
                }

                if (_run) _udp.BeginReceive(receiveCallback, null);
                else tcs.SetResult(null);
            };

            _udp.BeginReceive(receiveCallback, null);
            OnEvent(OnListening, EventArgs.Empty);
            await tcs.Task;
        }

        public void Dispose()
        {
            Dispose(true);
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
                    _udp?.Dispose();
                }
            }
        }

        private void OnError(Exception e)
        {
            OnEvent(OnErrored, new ErroredEventArgs(e));
        }

        private async void HandleRequest(byte[] data, IPEndPoint remote)
        {
            Protocol.DnsRequestMessage request = null;

            try
            {
                request = Protocol.DnsRequestMessage.FromArray(data);

                OnEvent(OnRequested, new RequestedEventArgs(request, data, remote));

                IResponse response = await _coder.Code(request);

                OnEvent(OnResponded, new RespondedEventArgs(request, response, data, remote));

                await _udp.SendAsync(response.ToArray(), response.Size, remote).WithCancellationTimeout(TimeSpan.FromMilliseconds(UDP_TIMEOUT));
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
                    response = Protocol.DnsResponseMessage.FromRequest(request);
                }

                try
                {
                    await _udp
                        .SendAsync(response.ToArray(), response.Size, remote)
                        .WithCancellationTimeout(TimeSpan.FromMilliseconds(UDP_TIMEOUT));
                }
                catch (SocketException) { }
                catch (OperationCanceledException) { }
                finally { OnError(e); }
            }
        }
    }
}
