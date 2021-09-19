/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.FileSocket
*文件名： Class1
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common.Serialization;
using SAEA.FileSocket.Model;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
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
                .UseIocp(new Context())
                .SetPort(port)
                .ReusePort(false)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetCount(count)                
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnReceive += _server_OnReceive;

            _server.OnError += _server_OnError;
        }

        private void _server_OnError(string ID, System.Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void _server_OnReceive(object currentObj, byte[] data)
        {
            var userToken = (IUserToken)currentObj;

            userToken.Unpacker.Unpack(data, (s) =>
            {
                string fileName = string.Empty;

                long length = 0;

                if (s.Content != null)
                {
                    var fi = s.Content.ToInstance<FileMessage>();
                    fileName = fi.FileName;
                    length = fi.Length;
                }

                OnRequested?.Invoke(userToken.ID, fileName, length);

                _total = length;
            }, null, (f) =>
            {
                Interlocked.Add(ref _in, f.Length);
                OnFile?.Invoke(userToken, f);
            });
        }

        public void Allow(string id)
        {
            var sm = new BaseSocketProtocal()
            {
                BodyLength = 0,
                Type = (byte)SocketProtocalType.AllowReceive
            };

            var data = sm.ToBytes();

            _server.SendAsync(id, data);
        }

        public void Refuse(string id)
        {
            var sm = new BaseSocketProtocal()
            {
                BodyLength = 0,
                Type = (byte)SocketProtocalType.RefuseReceive
            };

            var data = sm.ToBytes();

            _server.SendAsync(id, data);
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
