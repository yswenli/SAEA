/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.FileSocket
*文件名： Class1
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.FileSocket.Model;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace SAEA.FileSocket
{
    public class Client
    {
        DateTime Actived;

        int HeartSpan = 10 * 1000;

        int _bufferSize = 100 * 1024;

        byte[] _buffer;

        private long _total;

        private long _out;
        public long Total { get => _total; set => _total = value; }
        public long Out { get => _out; set => _out = value; }

        public bool Connected { get { return _client.Connected; } }


        ConcurrentStack<Action<bool>> _eventCollection = new ConcurrentStack<Action<bool>>();

        AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        Context _context;

        IClientSocket _client;

        public Client(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            _context = new Context();

            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp(_context)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnReceive += _client_OnReceive;

            _bufferSize = bufferSize;
            _buffer = new byte[_bufferSize];
            HeartAsync();
        }

        private void _client_OnReceive(byte[] data)
        {
            if (data != null)
            {
                _context.Unpacker.Unpack(data, (allow) =>
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
                        if (_client.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                var sm = new BaseSocketProtocal()
                                {
                                    BodyLength = 0,
                                    Type = (byte)SocketProtocalType.Heart
                                };
                                _client.SendAsync(sm.ToBytes());
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
            var data = BaseSocketProtocal.ParseRequest(content).ToBytes();
            _client.SendAsync(data);
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

                            var data = BaseSocketProtocal.ParseStream(content).ToBytes();

                            _client.SendAsync(data);

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

        public void Connect()
        {
            _client.Connect();
        }
    }
}
