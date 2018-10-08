/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Net
*文件名： RClient
*版本号： V2.1.5.0
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
*版本号： V2.1.5.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Serialize;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RPC.Net
{
    internal class RClient : BaseClientSocket, ISyncBase, IDisposable
    {
        bool _isConnected = false;

        bool _isDisposed = false;

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public bool IsConnected { get => _isConnected; set => _isConnected = value; }


        SyncHelper<byte[]> _syncHelper = new SyncHelper<byte[]>();


        object _syncLocker = new object();
        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        public RClient(Uri uri) : this(100 * 1024, uri.Host, uri.Port)
        {
            if (string.IsNullOrEmpty(uri.Scheme) || string.Compare(uri.Scheme, "rpc", true) != 0)
            {
                ExceptionCollector.Add("Consumer", new RPCSocketException("当前连接协议不正确，请使用格式rpc://ip:port"));
                return;
            }
        }

        public RClient(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new RContext(), ip, port, bufferSize)
        {

        }

        /// <summary>
        /// 连接到rpc服务
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            AutoResetEvent autoRestEvent = new AutoResetEvent(false);

            base.ConnectAsync((s) =>
            {
                if (s == System.Net.Sockets.SocketError.Success)
                {
                    _isConnected = true;
                    autoRestEvent.Set();
                }
            });

            return autoRestEvent.WaitOne(10 * 1000);
        }

        protected override void OnReceived(byte[] data)
        {
            ((RCoder)UserToken.Coder).Unpack(data, (msg) =>
            {
                //ConsoleHelper.WriteLine($"4 consumer receive: {msg.SequenceNumber}");

                switch ((RSocketMsgType)msg.Type)
                {
                    case RSocketMsgType.Ping:
                        break;
                    case RSocketMsgType.Pong:

                        break;
                    case RSocketMsgType.Request:
                        break;
                    case RSocketMsgType.Response:
                        _syncHelper.Set(msg.SequenceNumber, msg.Data);
                        break;
                    case RSocketMsgType.Error:
                        var offset = 0;
                        ExceptionCollector.Add("Consumer", new Exception((string)ParamsSerializeUtil.Deserialize(typeof(string), msg.Data, ref offset)));
                        _syncHelper.Set(msg.SequenceNumber, msg.Data);
                        break;
                    case RSocketMsgType.Close:
                        break;
                }
            });
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        internal void KeepAlive()
        {
            ThreadHelper.Run(() =>
            {
                while (!_isDisposed)
                {
                    try
                    {
                        if (_isConnected)
                        {
                            if (UserToken.Actived.AddSeconds(20) < DateTimeHelper.Now)
                            {
                                Send(new RSocketMsg(RSocketMsgType.Ping));
                            }
                        }
                        ThreadHelper.Sleep(5 * 100);
                    }
                    catch (Exception ex)
                    {
                        _isConnected = false;
                        ExceptionCollector.Add("Consumer", ex);
                    }
                }
            }, true, ThreadPriority.Highest);
        }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="msg"></param>
        internal void Send(RSocketMsg msg)
        {
            lock (_syncLocker)
            {
                if (_isConnected)
                {
                    var data = ((RCoder)UserToken.Coder).Encode(msg);
                    SendAsync(data);
                }
            }
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public byte[] Request(string serviceName, string method, byte[] args, int timeOut)
        {
            try
            {
                byte[] result = null;

                var msg = new RSocketMsg(RSocketMsgType.Request, serviceName, method)
                {
                    SequenceNumber = UniqueKeyHelper.Next()
                };

                msg.Data = args;

                if (_syncHelper.Wait(msg.SequenceNumber, () =>
                {
                    this.Send(msg); //ConsoleHelper.WriteLine($"1 consumer send: {msg.SequenceNumber}");
                },
                                        (r) => { result = r; },
                                        timeOut))
                {
                    return result;
                }
                else
                {
                    ExceptionCollector.Add("Consumer", new RPCSocketException($"serviceName:{serviceName}/method:{method} 调用超时！"));
                }
            }
            catch (Exception ex)
            {
                ExceptionCollector.Add("Consumer", new RPCSocketException($"serviceName:{serviceName}/method:{method} 调用出现异常！", ex));
            }
            return null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public new void Dispose()
        {
            _isDisposed = true;
            if (this._isConnected)
            {
                _isConnected = false;
                this.Disconnect();
            }
            base.Dispose();
        }
    }
}
