/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.QueueSocket
*文件名： QClient
*版本号： v5.0.0.1
*唯一标识：3806bd74-f304-42b2-ab04-3e219828fa60
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 16:16:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 16:16:57
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Threading;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.QueueSocket
{
    public class QClient
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        public event Action<QueueResult> OnMessage;

        QContext _qContext;

        QUnpacker _qUnpacker;

        QueueCoder _queueCoder;

        bool _isClosed = false;

        private AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        Batcher<byte[]> _batcher;

        IClientSocket _clientSocket;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        public bool Connected
        {
            get
            {
                return _clientSocket.Connected;
            }
        }

        public QClient(string name, string ipPort) : this(name, 102400, ipPort.ToIPPort().Item1, ipPort.ToIPPort().Item2)
        {

        }

        public QClient(string name, int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            _name = name;

            HeartSpan = 1000 * 1000;

            HeartAsync();

            _batcher = new Batcher<byte[]>(10000, 500);

            _batcher.OnBatched += _batcher_OnBatched;

            _qContext = new QContext();

            _qUnpacker = (QUnpacker)_qContext.Unpacker;

            _queueCoder = new QueueCoder();

            ISocketOption socketOption = SocketOptionBuilder.Instance.SetSocket(Sockets.Model.SAEASocketType.Tcp)
                .UseIocp(_qContext)
                .SetIP(ip)
                .SetPort(port)
                .SetWriteBufferSize(bufferSize)
                .SetReadBufferSize(bufferSize)
                .ReusePort(false)
                .Build();

            _clientSocket = SocketFactory.CreateClientSocket(socketOption);

            _clientSocket.OnReceive += _clientSocket_OnReceive;

            _clientSocket.OnError += _clientSocket_OnError;

            _clientSocket.OnDisconnected += _clientSocket_OnDisconnected;
        }


        private void _clientSocket_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void _clientSocket_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }

        /// <summary>
        /// 连接
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _clientSocket.ConnectAsync(callBack);
        }

        private void _clientSocket_OnReceive(byte[] data)
        {
            Actived = DateTimeHelper.Now;

            _qUnpacker.GetQueueResult(data, (r) =>
            {
                OnMessage?.Invoke(r);
            });
        }

        private void _batcher_OnBatched(IBatcher batcher, List<byte[]> data)
        {
            if (data != null && data.Any())
            {
                var list = new List<byte>();

                foreach (var item in data)
                {
                    list.AddRange(item);
                }

                _clientSocket.SendAsync(list.ToArray());

                list.Clear();
            }
        }

        private void HeartAsync()
        {
            TaskHelper.LongRunning(() =>
            {
                try
                {
                    while (!_isClosed)
                    {
                        if (_clientSocket.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                _clientSocket.SendAsync(_queueCoder.Ping(_name));
                            }
                            autoResetEvent.WaitOne(HeartSpan / 2);
                        }
                        else
                        {
                            autoResetEvent.WaitOne(1000);
                        }
                    }
                }
                catch { }
            });
        }

        #region 生产者发送消息

        /// <summary>
        /// 生产者发送消息
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="content"></param>
        public void Publish(string topic, string content)
        {
            _batcher.Insert(_queueCoder.Publish(_name, topic, content));
        }

        #endregion


        public void Subscribe(string topic)
        {
            _clientSocket.SendAsync(_queueCoder.Subscribe(_name, topic));
        }

        public void Unsubscribe(string topic)
        {
            _clientSocket.SendAsync(_queueCoder.Unsubcribe(_name, topic));
        }

        public void Close(int wait = 10000)
        {
            _isClosed = true;
            _clientSocket.SendAsync(_queueCoder.Close(_name));
        }
    }
}
