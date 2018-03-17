/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：ASEA.QueueSocket
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
using System.Collections.Generic;
using System.Threading;

namespace ASEA.QueueSocket
{
    public class QClient : BaseClientSocket
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        BatchProcess<byte[]> _batchProcess = null;

        object _locker = new object();

        public event Action<List<byte[]>> OnMessage;

        public QClient(string name, string ipPort) : this(name, 102400, ipPort.GetIPPort().Item1, ipPort.GetIPPort().Item2)
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

            UserToken.Coder.Pack(data, null, (s) =>
            {
                switch (s.Type)
                {
                    case (byte)QueueSocketMsgType.Ping:
                    case (byte)QueueSocketMsgType.Pong:
                        break;
                    case (byte)QueueSocketMsgType.Publish:
                        break;
                    case (byte)QueueSocketMsgType.PublishForBatch:
                        break;
                    case (byte)QueueSocketMsgType.Data:
                        OnMessage?.Invoke(s.Content.ToList());
                        break;
                    case (byte)QueueSocketMsgType.Unsubcribe:
                        break;
                    case (byte)QueueSocketMsgType.Close:
                        this.Disconnect();
                        break;
                }
            }, null);
        }

        private void HeartAsync()
        {
            ThreadHelper.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        if (this.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                var sm = new QueueSocketMsg()
                                {
                                    BodyLength = 0,
                                    Type = (byte)QueueSocketMsgType.Ping
                                };
                                SendAsync(sm.ToBytes());
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

        private void SendBase(QueueSocketMsgType type, byte[] content)
        {
            var data = QueueSocketMsg.Parse(content, type).ToBytes();

            this.Send(data);

            Actived = DateTimeHelper.Now;
        }

        private void SendAsyncBase(QueueSocketMsgType type, byte[] content)
        {
            var data = QueueSocketMsg.Parse(content, type).ToBytes();

            this.SendAsync(data);

            Actived = DateTimeHelper.Now;
        }


        public void Publish(string topic, byte[] content)
        {
            var pInfo = new PublishInfo()
            {
                Name = _name,
                Topic = topic,
                Data = content
            };
            SendAsyncBase(QueueSocketMsgType.Publish, pInfo.ToBytes());
        }



        public void PublishAsync(string topic, byte[] content, int maxNum = 500, int maxTime = 500)
        {
            var pInfo = new PublishInfo()
            {
                Name = _name,
                Topic = topic,
                Data = content
            };

            var cdata = pInfo.ToBytes();

            lock (_locker)
            {
                if (_batchProcess == null)
                {
                    _batchProcess = new BatchProcess<byte[]>((data) =>
                    {
                        SendBase(QueueSocketMsgType.PublishForBatch, data.ToBytes());
                    }, maxNum, maxTime);
                }
            }
            _batchProcess.Package(cdata);
        }

        public void PublishList(List<Tuple<string, byte[]>> data)
        {
            if (data == null) return;

            var list = new List<byte[]>();

            data.ForEach(item =>
            {
                var pInfo = new PublishInfo()
                {
                    Name = _name,
                    Topic = item.Item1,
                    Data = item.Item2
                };
                list.Add(pInfo.ToBytes());
            });
            SendBase(QueueSocketMsgType.PublishForBatch, list.ToBytes());
        }


        public void Subscribe(string topic)
        {
            var sInfo = new SubscribeInfo()
            {
                Name = _name,
                Topic = topic
            };
            var data = sInfo.ToBytes();
            SendBase(QueueSocketMsgType.Subcribe, data);
        }

        public void Unsubscribe(string topic)
        {
            var sInfo = new SubscribeInfo()
            {
                Name = _name,
                Topic = topic
            };
            var data = sInfo.ToBytes();
            SendBase(QueueSocketMsgType.Unsubcribe, data);
        }
    }
}
