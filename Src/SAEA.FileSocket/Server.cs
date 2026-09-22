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
*文件名： Server
*版本号： v26.4.23.1
*唯一标识：507c5630-17bc-479d-a611-7870e09ead35
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：Server服务端类
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：Server服务端类
*
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Common.Serialization;
using SAEA.FileSocket.Model;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Buffers;
using System.Threading;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 服务器
    /// 采用默认的上下文操作
    /// </summary>
    public class Server
    {
        #region events

        public event OnRequestHandler OnRequested;

        public event OnFileHandler OnFile;

        public event OnErrorHandler OnError;

        #endregion

        private long _total;

        private long _in;

        public long Total { get => _total; set => _total = value; }
        public long In { get => _in; set => _in = value; }

        IServerSocket _server;

        public Server(int port = 39654, int bufferSize = 100 * 1024, int count = 10)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp<BaseCoder>()
                .SetPort(port)
                .ReusePort(false)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetMaxConnects(count)                
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnServerReceiveSpan += _server_OnReceiveSpan;

            _server.OnError += _server_OnError;
        }

        private void _server_OnError(string ID, System.Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void _server_OnReceiveSpan(IUserToken currentObj, ReadOnlySpan<byte> dataSpan)
        {
            var userToken = currentObj;

            var data = dataSpan.ToArray();

            using (var msgs = userToken.Coder.Decode(new ReadOnlySequence<byte>(data), null, (f) =>
            {
                Interlocked.Add(ref _in, f.Length);
                OnFile?.Invoke(userToken, f.ToArray());
            }))
            {
                if (msgs.Count < 1) return;
                foreach (var msg in msgs.Frames)
                {
                    string fileName = string.Empty;

                    long length = 0;

                    if (msg.Content.Length != 0)
                    {
                        var fi = SerializeHelper.PBDeserialize<FileMessage>(msg.Content.ToArray());
                        fileName = fi.FileName;
                        length = fi.Length;
                    }

                    OnRequested?.Invoke(userToken.ID, fileName, length);

                    _total = length;
                }
            }
        }

        public void Allow(string id)
        {
            var sm = new BaseSocketProtocal((byte)SocketProtocalType.AllowReceive, ReadOnlyMemory<byte>.Empty);

            using (var w = new PooledBufferWriter(64))
            {
                sm.WriteTo(w);
                _server.SendAsync(id, w.WrittenSpan.ToArray().AsMemory());
            }
        }

        public void Refuse(string id)
        {
            var sm = new BaseSocketProtocal((byte)SocketProtocalType.RefuseReceive, ReadOnlyMemory<byte>.Empty);

            using (var w = new PooledBufferWriter(64))
            {
                sm.WriteTo(w);
                _server.SendAsync(id, w.WrittenSpan.ToArray().AsMemory());
            }
        }


        public void Start()
        {
            _server.Start();
        }

        public void Stop()
        {
            _server.Stop();
        }
    }
}