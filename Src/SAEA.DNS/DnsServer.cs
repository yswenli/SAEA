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
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using SAEA.DNS.Coder;
using SAEA.DNS.Model;
using SAEA.DNS.Protocol;
using SAEA.DNS.Common.Utils;
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

        public event EventHandler<RequestedEventArgs> Requested;
        public event EventHandler<RespondedEventArgs> Responded;
        public event EventHandler<EventArgs> Listening;
        public event EventHandler<ErroredEventArgs> Errored;

        private bool run = true;
        private bool disposed = false;
        private UdpClient udp;
        private IRequestCoder resolver;

        public DnsServer(DnsDataFile masterFile, IPEndPoint endServer) :
            this(new FallbackRequestResolver(masterFile, new UdpRequestCoder(endServer)))
        { }

        public DnsServer(DnsDataFile masterFile, IPAddress endServer, int port = DEFAULT_PORT) :
            this(masterFile, new IPEndPoint(endServer, port))
        { }

        public DnsServer(DnsDataFile masterFile, string endServer="119.29.29.29", int port = DEFAULT_PORT) :
            this(masterFile, IPAddress.Parse(endServer), port)
        { }

        public DnsServer(IPEndPoint endServer) :
            this(new UdpRequestCoder(endServer))
        { }

        public DnsServer(IPAddress endServer, int port = DEFAULT_PORT) :
            this(new IPEndPoint(endServer, port))
        { }

        public DnsServer(string endServer, int port = DEFAULT_PORT) :
            this(IPAddress.Parse(endServer), port)
        { }

        public DnsServer(IRequestCoder resolver)
        {
            this.resolver = resolver;
        }

        public Task Listen(int port = DEFAULT_PORT, IPAddress ip = null)
        {
            return Listen(new IPEndPoint(ip ?? IPAddress.Any, port));
        }

        public async Task Listen(IPEndPoint endpoint)
        {
            await Task.Yield();

            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

            if (run)
            {
                try
                {
                    udp = new UdpClient(endpoint);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
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
                    data = udp.EndReceive(result, ref remote);
                    HandleRequest(data, remote);
                }
                catch (ObjectDisposedException)
                {
                    // 运行应该已经是错误的
                    run = false;
                }
                catch (SocketException e)
                {
                    OnError(e);
                }

                if (run) udp.BeginReceive(receiveCallback, null);
                else tcs.SetResult(null);
            };

            udp.BeginReceive(receiveCallback, null);
            OnEvent(Listening, EventArgs.Empty);
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
            if (!disposed)
            {
                disposed = true;

                if (disposing)
                {
                    run = false;
                    udp?.Dispose();
                }
            }
        }

        private void OnError(Exception e)
        {
            OnEvent(Errored, new ErroredEventArgs(e));
        }

        private async void HandleRequest(byte[] data, IPEndPoint remote)
        {
            Request request = null;

            try
            {
                request = Request.FromArray(data);
                OnEvent(Requested, new RequestedEventArgs(request, data, remote));

                IResponse response = await resolver.Resolve(request);

                OnEvent(Responded, new RespondedEventArgs(request, response, data, remote));
                await udp
                    .SendAsync(response.ToArray(), response.Size, remote)
                    .WithCancellationTimeout(TimeSpan.FromMilliseconds(UDP_TIMEOUT));
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
                    response = Response.FromRequest(request);
                }

                try
                {
                    await udp
                        .SendAsync(response.ToArray(), response.Size, remote)
                        .WithCancellationTimeout(TimeSpan.FromMilliseconds(UDP_TIMEOUT));
                }
                catch (SocketException) { }
                catch (OperationCanceledException) { }
                finally { OnError(e); }
            }
        }

        public class RequestedEventArgs : EventArgs
        {
            public RequestedEventArgs(IRequest request, byte[] data, IPEndPoint remote)
            {
                Request = request;
                Data = data;
                Remote = remote;
            }

            public IRequest Request
            {
                get;
                private set;
            }

            public byte[] Data
            {
                get;
                private set;
            }

            public IPEndPoint Remote
            {
                get;
                private set;
            }
        }

        public class RespondedEventArgs : EventArgs
        {
            public RespondedEventArgs(IRequest request, IResponse response, byte[] data, IPEndPoint remote)
            {
                Request = request;
                Response = response;
                Data = data;
                Remote = remote;
            }

            public IRequest Request
            {
                get;
                private set;
            }

            public IResponse Response
            {
                get;
                private set;
            }

            public byte[] Data
            {
                get;
                private set;
            }

            public IPEndPoint Remote
            {
                get;
                private set;
            }
        }

        public class ErroredEventArgs : EventArgs
        {
            public ErroredEventArgs(Exception e)
            {
                Exception = e;
            }

            public Exception Exception
            {
                get;
                private set;
            }
        }

        private class FallbackRequestResolver : IRequestCoder
        {
            private IRequestCoder[] resolvers;

            public FallbackRequestResolver(params IRequestCoder[] resolvers)
            {
                this.resolvers = resolvers;
            }

            public async Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                IResponse response = null;

                foreach (IRequestCoder resolver in resolvers)
                {
                    response = await resolver.Resolve(request, cancellationToken);
                    if (response.AnswerRecords.Count > 0) break;
                }

                return response;
            }
        }
    }
}
