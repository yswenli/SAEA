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
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SAEA.QueueSocket
{
    public class QClient : BaseClientSocket
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        BatchProcess<byte[]> _batchProcess = null;

        object _locker = new object();

        public event Action<QueueResult> OnMessage;


        QueueCoder queueCoder = new QueueCoder();

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
                    while (true)
                    {
                        if (this.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                SendAsync(queueCoder.Ping(_name));
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


        public void Publish(string topic, string content)
        {
            Send(queueCoder.Publish(_name, topic, content));
        }

        public void PublishList(string topic,string[] content)
        {
            Send(queueCoder.PublishForBatch(_name, topic, content));
        }


        public void Subscribe(string topic)
        {
            SendAsync(queueCoder.Subscribe(_name, topic));
        }

        public void Unsubscribe(string topic)
        {
            SendAsync(queueCoder.Unsubcribe(_name, topic));
        }

        public void Close()
        {
            SendAsync(queueCoder.Close(_name));
        }
    }
}
