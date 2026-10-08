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
*命名空间：SAEA.QueueSocket
*文件名： QClient
*版本号： v26.4.23.1
*唯一标识：25a2f16c-e628-4aec-826f-d09936146044
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：QClient接口
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：QClient接口
*
*****************************************************************************/
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Threading;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets;
using SAEA.Sockets.Handler;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 队列客户端类
    /// </summary>
    public class QClient
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        byte[] _nameBytes;

        public event Action<QueueMsg> OnMessage;

        Net.QueueCoder _queueCoder;

        bool _isClosed = false;

        private AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        PooledBatcher _batcher;

        IClientSocket _clientSocket;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 获取客户端是否已连接
        /// </summary>
        public bool Connected
        {
            get
            {
                return _clientSocket.Connected;
            }
        }

        /// <summary>
        /// 构造函数，初始化QClient实例
        /// </summary>
        /// <param name="name">客户端名称</param>
        /// <param name="ipPort">IP和端口</param>
        public QClient(string name, string ipPort) : this(name, 128 * 1024, ipPort.ToIPPort().Item1, ipPort.ToIPPort().Item2)
        {

        }

        /// <summary>
        /// 构造函数，初始化QClient实例
        /// </summary>
        /// <param name="name">客户端名称</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口号</param>
        public QClient(string name, int bufferSize = 128 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            QueueSocketThreadPool.EnsureConfigured();

            _name = name;

            _nameBytes = string.IsNullOrEmpty(name) ? null : Encoding.UTF8.GetBytes(name);

            HeartSpan = 60 * 1000;

            HeartAsync();

            _batcher = new PooledBatcher(1000, 50);

            _batcher.OnBatched += _batcher_OnBatched;

            _queueCoder = new QueueCoder();

            ISocketOption socketOption = SocketOptionBuilder.Instance.SetSocket(Sockets.Model.SAEASocketType.Tcp)
                .UseIocp<QueueCoder>()
                .SetIP(ip)
                .SetPort(port)
                .SetWriteBufferSize(bufferSize)
                .SetReadBufferSize(bufferSize)
                .ReusePort(false)
                .Build();

            _clientSocket = SocketFactory.CreateClientSocket(socketOption);

            _clientSocket.OnClientReceiveSpan += _clientSocket_OnReceiveSpan;

            _clientSocket.OnError += _clientSocket_OnError;

            _clientSocket.OnDisconnected += _clientSocket_OnDisconnected;
        }

        /// <summary>
        /// 错误处理事件
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="ex">异常信息</param>
        private void _clientSocket_OnError(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// 断开连接处理事件
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="ex">异常信息</param>
        private void _clientSocket_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnDisconnected?.Invoke(id, ex);
        }

        /// <summary>
        /// 连接
        /// </summary>
        public void Connect()
        {
            _clientSocket.Connect();
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        /// <param name="callBack">回调函数</param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _clientSocket.ConnectAsync(callBack);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _clientSocket.Disconnect();
        }

        /// <summary>
        /// 接收数据处理事件
        /// </summary>
        /// <param name="data">接收到的数据</param>
        private void _clientSocket_OnReceiveSpan(ReadOnlySpan<byte> dataSpan)
        {
            Actived = DateTimeHelper.Now;
            var list = _queueCoder.GetQueueResult(dataSpan);
            if (list != null)
            {
                foreach (var item in list)
                {
                    OnMessage?.Invoke(item);
                }
                QueueMsgListPool.Return(list);
            }
        }

        /// <summary>
        /// 消息提交事件：消息已提交给 socket 发送（所有权已移交），并非对端已确认收到。
        /// </summary>
        public event Action<int> OnMessagesSent;

        /// <summary>
        /// 批量处理事件。writer 所有权随回调转移给 socket，发送完成后由 socket 释放。
        /// </summary>
        /// <param name="writer">合并后的写入器</param>
        /// <param name="count">本批消息数量</param>
        private void _batcher_OnBatched(PooledBufferWriter writer, int count)
        {
            _clientSocket.SendAsync(writer.WrittenMemory, writer);
            try
            {
                OnMessagesSent?.Invoke(count);
            }
            catch { }
        }

        /// <summary>
        /// 心跳异步处理
        /// </summary>
        private void HeartAsync()
        {
            TaskHelper.LongRunning(() =>
            {
                try
                {
                    while (!_isClosed)
                    {
                        if (_clientSocket != null && _clientSocket.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                _clientSocket.Send(_queueCoder.Ping(_name).AsSpan());
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
        /// <param name="topic">主题</param>
        /// <param name="content">内容</param>
        public void Publish(string topic, string content)
        {
            var topicBytes = string.IsNullOrEmpty(topic) ? null : Encoding.UTF8.GetBytes(topic);
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var nameLen = _nameBytes == null ? 0 : _nameBytes.Length;
            var topicLen = topicBytes == null ? 0 : topicBytes.Length;
            var writer = new PooledBufferWriter(1 + 12 + nameLen + topicLen + contentBytes.Length);
            QueueCoder.WriteFrameTo(writer, QueueSocketMsgType.Publish, _nameBytes, topicBytes, contentBytes);
            if (!_batcher.Insert(writer))
            {
                writer.Dispose();
            }
        }

        #endregion

        #region 订阅者

        /// <summary>
        /// 订阅主题
        /// </summary>
        /// <param name="topic">主题</param>
        public void Subscribe(string topic)
        {
            _clientSocket.Send(_queueCoder.Subscribe(_name, topic).AsSpan());
        }

        /// <summary>
        /// 取消订阅主题
        /// </summary>
        /// <param name="topic">主题</param>
        public void Unsubscribe(string topic)
        {
            _clientSocket.Send(_queueCoder.Unsubcribe(_name, topic).AsSpan());
        }

        #endregion

        /// <summary>
        /// 关闭客户端
        /// </summary>
        /// <param name="wait">等待时间</param>
        public void Close(int wait = 10000)
        {
            _isClosed = true;
            _batcher.OnBatched -= _batcher_OnBatched;
            _batcher?.Dispose();
            _clientSocket.Send(_queueCoder.Close(_name).AsSpan());
            _clientSocket.Disconnect();
        }

        /// <summary>
        /// 清除编码器缓冲区
        /// </summary>
        public void ClearCoderBuffer()
        {
            _queueCoder?.Clear();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close();
            _queueCoder = null;
        }
    }
}