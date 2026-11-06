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
*唯一标识：2a41eb6e-03dc-4ae0-bd0e-9cde3876dae3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：Client接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:18
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 文件传输客户端，按包模式连接服务器并按块发送文件，构造后自动启动心跳线程
    /// </summary>
    public class Client
    {
        DateTime Actived;

        int HeartSpan = 10 * 1000;

        int _bufferSize = 100 * 1024;

        byte[] _buffer;

        private long _total;

        private long _out;
        /// <summary>
        /// 待发送文件的总字节数，发送前由文件长度赋值
        /// </summary>
        public long Total { get => _total; set => _total = value; }

        /// <summary>
        /// 已发送的字节数，发送过程中以原子方式累加
        /// </summary>
        public long Out { get => _out; set => _out = value; }

        /// <summary>
        /// 与服务器的连接是否已建立
        /// </summary>
        public bool Connected { get { return _client.Connected; } }


        ConcurrentStack<Action<bool>> _eventCollection = new ConcurrentStack<Action<bool>>();

        AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        IClientSocket _client;

        BaseCoder _unpacker;

        /// <summary>
        /// 创建客户端并注册接收回调，同时启动后台心跳线程
        /// </summary>
        /// <param name="bufferSize">收发缓冲区大小，同时也是文件分块读取的大小（字节）</param>
        /// <param name="ip">服务器地址</param>
        /// <param name="port">服务器端口</param>
        public Client(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UsePacket<BaseCoder>()
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnClientReceiveSpan += _client_OnReceiveSpan;

            _bufferSize = bufferSize;

            _buffer = new byte[_bufferSize];

            _unpacker = new BaseCoder();

            HeartAsync();
        }

        private void _client_OnReceiveSpan(ReadOnlySpan<byte> dataSpan)
        {
            var data = dataSpan.ToArray();

            using (var msgs = _unpacker.Decode(new ReadOnlySequence<byte>(data), null, null))
            {
                if (msgs.Count == 0)
                {
                    return;
                }
                foreach (var msg in msgs.Frames)
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
                                var sm = new BaseSocketProtocal((byte)SocketProtocalType.Heart, ReadOnlyMemory<byte>.Empty);
                                using (var w = new PooledBufferWriter(64))
                                {
                                    sm.WriteTo(w);
                                    _client.Send(w.WrittenSpan);
                                }
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
            using (var w = new PooledBufferWriter(64))
            {
                BaseSocketProtocal.ParseRequest(content).WriteTo(w);
                _client.Send(w.WrittenSpan);
            }
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
                                using (var w = new PooledBufferWriter(64))
                                {
                                    BaseSocketProtocal.ParseStream(contentSpan.ToArray()).WriteTo(w);
                                    _client.SendAsync(w.WrittenSpan.ToArray().AsMemory());
                                }

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

        /// <summary>
        /// 同步连接服务器
        /// </summary>
        public void Connect()
        {
            _client.Connect();
        }

        /// <summary>
        /// 断开与服务器的连接
        /// </summary>
        public void Disconnect()
        {
            _client.Disconnect();
        }
    }
}