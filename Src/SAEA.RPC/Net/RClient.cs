/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Net
*文件名： RClient
*版本号： v5.0.0.1
*唯一标识：6921ced2-8a62-45a7-89c6-84d1301c1a28
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 16:16:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 16:16:42
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.RPC.Net
{
    internal class RClient : ISyncBase, IDisposable
    {
        bool _isDisposed = false;

        SyncHelper<byte[]> _syncHelper;

        RContext _RContext;

        IClientSocket _client;

        public object SyncLocker { get; } = new object();

        public bool Connected
        {
            get
            {
                return _client == null ? false : _client.Connected;
            }
        }

        public event OnNoticedHandler OnNoticed;

        public event OnDisconnectedHandler OnDisconnected;

        public event OnErrorHandler OnError;

        int _timeOut = 3000;

        public RClient(Uri uri, int timeOut = 3000)
        {
            if (string.IsNullOrEmpty(uri.Scheme) || string.Compare(uri.Scheme, "rpc", true) != 0)
            {
                ExceptionCollector.Add("Consumer.RClient.Init Error", new RPCSocketException("当前连接协议不正确，请使用格式rpc://ip:port"));
                return;
            }

            _timeOut = timeOut;

            _syncHelper = new SyncHelper<byte[]>(_timeOut);

            var ipPort = DNSHelper.GetIPPort(uri);

            _RContext = new RContext();

            SocketOptionBuilder builder = SocketOptionBuilder.Instance;

            var option = builder.SetSocket()
                .UseIocp(_RContext)
                .SetIP(ipPort.Item1)
                .SetPort(ipPort.Item2)
                .SetReadBufferSize()
                .SetWriteBufferSize()
                .SetTimeOut(_timeOut)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnReceive += OnReceived;

            _client.OnDisconnected += _client_OnDisConnected;
        }

        private void _client_OnDisConnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }

        public void Connect()
        {
            _client.ConnectAsync();
        }

        protected void OnReceived(byte[] data)
        {
            try
            {
                ((RUnpacker)_RContext.Unpacker).Unpack(data, msg =>
                {
                    switch ((RSocketMsgType)msg.Type)
                    {
                        case RSocketMsgType.Ping:
                            break;
                        case RSocketMsgType.Pong:

                            break;
                        case RSocketMsgType.Request:
                            break;
                        case RSocketMsgType.Response:
                            _syncHelper.Set(msg.Data);
                            break;
                        case RSocketMsgType.Notice:
                            OnNoticed.Invoke(msg.Data);
                            break;
                        case RSocketMsgType.Error:
                            ExceptionCollector.Add("Consumer.OnReceived Error", new Exception(SAEASerialize.Deserialize<string>(msg.Data)));
                            _syncHelper.Set(msg.Data);
                            break;
                        case RSocketMsgType.Close:
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_client.Endpoint.ToString(), ex);
            }

        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        internal void KeepAlive()
        {
            TaskHelper.LongRunning(() =>
            {
                while (!_isDisposed)
                {
                    try
                    {
                        if (_client.Connected)
                        {
                            if (_RContext.UserToken.Actived.AddSeconds(60) < DateTimeHelper.Now)
                            {
                                Send(new RSocketMsg(RSocketMsgType.Ping));
                            }
                        }
                        ThreadHelper.Sleep(5 * 100);
                    }
                    catch (Exception ex)
                    {
                        ExceptionCollector.Add("Consumer.KeepAlive Error", ex);
                    }
                }
            });
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="msg"></param>
        internal void Send(RSocketMsg msg)
        {
            var data = ((RUnpacker)_RContext.Unpacker).Encode(msg);

            _client.SendAsync(data);
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public byte[] Request(string serviceName, string method, byte[] args)
        {
            byte[] result = null;

            try
            {
                var msg = new RSocketMsg(RSocketMsgType.Request, serviceName, method)
                {
                    SequenceNumber = UniqueKeyHelper.Next()
                };

                msg.Data = args;

                result = _syncHelper.Wait(() => { this.Send(msg); });
            }
            catch (Exception ex)
            {
                ExceptionCollector.Add("Consumer.Request.Error", new RPCSocketException($"serviceName:{serviceName}/method:{method} 调用超时！", ex));
            }
            return result;
        }

        /// <summary>
        /// 向rpc service provider 注册接收通知
        /// </summary>
        public void RegistReceiveNotice()
        {
            var msg = new RSocketMsg(RSocketMsgType.RegistNotice, null, null)
            {
                SequenceNumber = UniqueKeyHelper.Next()
            };
            Send(msg);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;
            _client.Dispose();
        }
    }
}
