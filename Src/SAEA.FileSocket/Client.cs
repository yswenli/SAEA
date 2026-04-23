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
*命名空间：SAEA.FileSocket
*文件名： Client
*版本号： v26.4.23.1
*唯一标识：ee0336ba-0982-4912-bc02-c87898797073
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
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

        IClientSocket _client;

        BaseCoder _unpacker;

        public Client(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp<BaseCoder>()
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnReceive += _client_OnReceive;

            _bufferSize = bufferSize;

            _buffer = new byte[_bufferSize];

            _unpacker = new BaseCoder();

            HeartAsync();
        }

        private void _client_OnReceive(byte[] data)
        {
            if (data != null)
            {
                var msgs = _unpacker.Decode(data, null, null);
                if (msgs == null || msgs.Count == 0)
                {
                    return;
                }
                foreach (var msg in msgs)
                {
                    Action<bool> action;

                    if (_eventCollection.TryPop(out action))
                    {
                        var result = false;

                        if (msg.Type == (byte)SocketProtocalType.AllowReceive)
                        {
                            result = true;
                        }

                        action?.Invoke(result);
                    }
                }
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
                                _client.Send(sm.ToBytes());
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
            _client.Send(data);
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
                byte[] buffer = null;

                try
                {
                    buffer = MemoryPoolManager.Rent(_bufferSize);

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
                                // Use Span to avoid creating new array
                                var contentSpan = buffer.AsSpan(0, readNum);
                                var data = BaseSocketProtocal.ParseStream(contentSpan.ToArray()).ToBytes();

                                _client.SendAsync(data);

                                Interlocked.Add(ref _out, readNum);
                            }
                            else
                                break;
                        }
                        while (true);
                    }
                }
                finally
                {
                    if (buffer != null)
                    {
                        MemoryPoolManager.Return(buffer, _bufferSize);
                    }
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

                var data = SerializeHelper.PBSerialize(new FileMessage() { FileName = fName, Length = new FileInfo(fileName).Length, Offset = offset });

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

        public void Disconnect()
        {
            _client.Disconnect();
        }
    }
}