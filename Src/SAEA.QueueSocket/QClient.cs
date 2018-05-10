/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.QueueSocket
*文件名： QClient
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Core;
using SAEA.Sockets.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.QueueSocket
{
    public class QClient : BaseClientSocket
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        object _locker = new object();

        public event Action<QueueResult> OnMessage;

        QueueCoder _queueCoder = new QueueCoder();

        ConcurrentQueue<Byte[]> _concurrentQueue = new ConcurrentQueue<byte[]>();


        bool _isClosed = false;

        public QClient(string name, string ipPort) : this(name, 102400, ipPort.GetIPPort().Item1, ipPort.GetIPPort().Item2)
        {

        }

        public QClient(string name, int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new QContext(), ip, port, bufferSize)
        {
            _name = name;

            HeartSpan = 1000 * 1000;

            HeartAsync();

            SendDataAsync();
        }


        protected override void OnReceived(byte[] data)
        {
            Actived = DateTimeHelper.Now;

            var qcoder = (QCoder)UserToken.Coder;

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
                            ThreadHelper.Sleep(HeartSpan / 2);
                        }
                        else
                        {
                            ThreadHelper.Sleep(1);
                        }
                    }
                }
                catch { }
            }, true, System.Threading.ThreadPriority.Highest);
        }

        private void SendDataAsync()
        {
            ThreadHelper.Run(() =>
            {
                try
                {
                    while (!_isClosed)
                    {
                        if (this.Connected && !_concurrentQueue.IsEmpty)
                        {
                            var list = new List<byte>();
                            while (_concurrentQueue.TryDequeue(out byte[] data))
                            {
                                list.AddRange(data);
                            }
                            if (list.Count > 0)
                                Send(list.ToArray());
                        }
                        else
                            ThreadHelper.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine("SendDataAsync error:" + ex.Message);
                }
            }, true, System.Threading.ThreadPriority.Highest);
        }

        public void Publish(string topic, string content)
        {
            while (_concurrentQueue.Count > 10000)
            {
                ThreadHelper.Sleep(1);
            }
            _concurrentQueue.Enqueue(_queueCoder.Publish(_name, topic, content));

            //Send(_queueCoder.Publish(_name, topic, content));
        }

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
                ThreadHelper.Sleep(10);
            }
            _isClosed = true;
            SendAsync(_queueCoder.Close(_name));
        }
    }
}
