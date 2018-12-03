/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.FileSocket
*文件名： Class1
*版本号： V3.3.3.5
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/

using SAEA.FileSocket.Model;
using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Model;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace SAEA.FileSocket
{
    public class Client : BaseClientSocket
    {
        DateTime Actived;

        int HeartSpan = 10 * 1000;

        int _bufferSize = 100 * 1024;

        byte[] _buffer;

        private long _total;

        private long _out;
        public long Total { get => _total; set => _total = value; }
        public long Out { get => _out; set => _out = value; }


        ConcurrentStack<Action<bool>> _eventCollection = new ConcurrentStack<Action<bool>>();

        AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        public Client(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new Context(), ip, port, bufferSize)
        {
            _bufferSize = bufferSize;
            _buffer = new byte[_bufferSize];
            HeartAsync();
        }

        /// <summary>
        /// 重写数据接收以实现具体的业务应用场景
        /// 自行实现ICotext、ICoder等可以实现自定义协议
        /// </summary>
        /// <param name="data"></param>
        protected override void OnReceived(byte[] data)
        {
            if (data != null)
            {
                this.UserToken.Unpacker.Unpack(data, (allow) =>
                {

                    Action<bool> action;

                    if (_eventCollection.TryPop(out action))
                    {
                        var result = false;

                        if (allow.Type == (byte)SocketProtocalType.AllowReceive)
                        {
                            result = true;
                        }

                        action?.Invoke(result);
                    }
                }, null, null);
            }
        }


        void HeartAsync()
        {
            new Thread(new ThreadStart(() =>
            {
                Actived = DateTimeHelper.Now;
                try
                {
                    while (true)
                    {
                        if (this.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                var sm = new SocketProtocal()
                                {
                                    BodyLength = 0,
                                    Type = (byte)SocketProtocalType.Heart
                                };
                                SendAsync(sm.ToBytes());
                            }
                            Thread.Sleep(HeartSpan);
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
                catch { }
            }))
            { IsBackground = true }.Start();
        }

        void sendMessageBase(byte[] content)
        {
            var data = SocketProtocal.ParseRequest(content).ToBytes();
            SendAsync(data);
        }


        /// <summary>
        /// 发送请求信息
        /// </summary>
        /// <param name="content"></param>
        /// <param name="onComlete"></param>
        void sendRequest(byte[] content, Action<bool> onComlete)
        {
            sendMessageBase(content);
            _eventCollection.Push(onComlete);
        }

        /// <summary>
        /// 发送文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="offset"></param>
        void sendFileBase(string fileName, long offset)
        {
            if (File.Exists(fileName))
            {
                var buffer = new byte[_bufferSize];

                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    _total = fs.Length;

                    int readNum = 0;

                    do
                    {
                        fs.Position = offset;

                        readNum = fs.Read(buffer, 0, _bufferSize);

                        offset += readNum;

                        if (readNum > 0)
                        {
                            var content = new byte[readNum];

                            Buffer.BlockCopy(buffer, 0, content, 0, readNum);

                            var data = SocketProtocal.ParseStream(content).ToBytes();

                            Send(data);

                            Interlocked.Add(ref _out, readNum);
                        }
                        else
                            break;
                    }
                    while (true);
                }
            }
        }

        /// <summary>
        /// 集成发送文件功能
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="offset"></param>
        /// <param name="complete"></param>
        public void SendFile(string fileName, long offset, Action<bool> complete)
        {
            _autoResetEvent.WaitOne();

            if (File.Exists(fileName))
            {
                var fName = Path.GetFileName(fileName);

                var data = SerializeHelper.ByteSerialize(new FileMessage() { FileName = fName, Length = new FileInfo(fileName).Length, Offset = offset });

                sendRequest(data, (d) =>
                {
                    if (d)
                    {
                        sendFileBase(fileName, offset);
                    }
                    complete?.Invoke(d);
                    _autoResetEvent.Set();
                });
            }
        }
    }
}
