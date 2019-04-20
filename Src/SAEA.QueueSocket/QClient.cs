/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.QueueSocket
*文件名： QClient
*版本号： v4.3.3.7
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
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.Sockets.Core.Tcp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.QueueSocket
{
    public class QClient : IocpClientSocket
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        object _locker = new object();

        public event Action<QueueResult> OnMessage;

        QueueCoder _queueCoder = new QueueCoder();

        ConcurrentQueue<Byte[]> _concurrentQueue = new ConcurrentQueue<byte[]>();

        bool _isClosed = false;

        private AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        public QClient(string name, string ipPort) : this(name, 102400, ipPort.ToIPPort().Item1, ipPort.ToIPPort().Item2)
        {

        }

        public QClient(string name, int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new QContext(), ip, port, bufferSize)
        {
            _name = name;

            HeartSpan = 1000 * 1000;

            HeartAsync();
        }


        protected override void OnReceived(byte[] data)
        {
            Actived = DateTimeHelper.Now;

            var qcoder = (QUnpacker)UserToken.Unpacker;

            qcoder.GetQueueResult(data, (r) =>
            {
                OnMessage?.Invoke(r);
            });
        }

        private void HeartAsync()
        {
            ThreadHelper.Run(() =>
            {
                try
                {
                    while (!_isClosed)
                    {
                        if (this.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                SendAsync(_queueCoder.Ping(_name));
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
            }, true, System.Threading.ThreadPriority.Highest);
        }

        #region 生产者发送消息

        private readonly List<byte> cache = new List<byte>();

        private DateTime _sendSpan = DateTimeHelper.Now.AddMilliseconds(500);

        private readonly object sendLock = new object();

        /// <summary>
        /// 生产者发送消息
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="content"></param>
        public void Publish(string topic, string content)
        {
            try
            {
                Monitor.Enter(sendLock);
                cache.AddRange(_queueCoder.Publish(_name, topic, content));
                if (cache.Count < 1000 * 10 || _sendSpan < DateTimeHelper.Now)
                {
                    return;
                }
                SendAsync(cache.ToArray());
                cache.Clear();
                _sendSpan = DateTimeHelper.Now.AddMilliseconds(500);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Monitor.Exit(sendLock);
            }
        }

        #endregion


        public void Subscribe(string topic)
        {
            SendAsync(_queueCoder.Subscribe(_name, topic));
        }

        public void Unsubscribe(string topic)
        {
            SendAsync(_queueCoder.Unsubcribe(_name, topic));
        }

        public void Close()
        {
            while (!_concurrentQueue.IsEmpty)
            {
                autoResetEvent.WaitOne(10000);
            }
            _isClosed = true;
            SendAsync(_queueCoder.Close(_name));
        }
    }
}
