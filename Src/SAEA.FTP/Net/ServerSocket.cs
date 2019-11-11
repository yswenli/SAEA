/****************************************************************************
*项目名称：SAEA.FTP.Net
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Net
*类 名 称：ServerSocket
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 11:25:32
*描述：
*=====================================================================
*修改时间：2019/11/11 11:25:32
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Net
{
    class ServerSocket : IDisposable
    {
        IServerSokcet _serverSocket;

        ServerConfig _serverConfig;

        FTPStream _ftpStream;

        public event Action OnReceived;

        public ServerConfig Config { get => _serverConfig; set => _serverConfig = value; }


        public ServerSocket(ServerConfig serverConfig)
        {
            _serverConfig = serverConfig;

            var option = SocketOptionBuilder.Instance
               .SetSocket()
               .UseIocp()
               .SetPort(_serverConfig.Port)
               .SetReadBufferSize(_serverConfig.BufferSize)
               .SetWriteBufferSize(_serverConfig.BufferSize)
               .Build();

            _serverSocket = SocketFactory.CreateServerSocket(option);

            _serverSocket.OnError += _serverSocket_OnError;

            _serverSocket.OnReceive += _serverSocket_OnReceive;

            _serverSocket.OnDisconnected += _serverSocket_OnDisconnected;

            _ftpStream = new FTPStream();
        }

        public void Start()
        {
            _serverSocket.Start();
        }

        public void Stop()
        {
            _serverSocket.Stop();
        }

        private void _serverSocket_OnError(string ID, Exception ex)
        {
            LogHelper.Error("FTPServer Error", ex);
        }

        private void _serverSocket_OnReceive(object currentObj, byte[] data)
        {
            _ftpStream.Write(data);

            var msg = _ftpStream.ReadLine();

            if (!string.IsNullOrWhiteSpace(msg))
            {
                OnReceived?.Invoke();
            }
        }
        private void _serverSocket_OnDisconnected(string ID, Exception ex)
        {
            throw new NotImplementedException();
        }

        public IServerSokcet CreateDataSocket(int port = 22)
        {
            var option = SocketOptionBuilder.Instance
               .SetSocket()
               .UseIocp()
               .SetPort(port)
               .SetReadBufferSize(_serverConfig.BufferSize)
               .SetWriteBufferSize(_serverConfig.BufferSize)
               .Build();
            var dataSocket = SocketFactory.CreateServerSocket(option);
            dataSocket.OnError += _serverSocket_OnError;
            dataSocket.OnReceive += DataSocket_OnReceive;
            dataSocket.Start();
            return dataSocket;
        }

        private void DataSocket_OnReceive(object currentObj, byte[] data)
        {
            var ut = currentObj as IUserToken;

            if(_serverConfig.Users.TryGetValue(ut.ID,out FTPUser ftpUser))
            {
                ftpUser.FTPDataManager.Receive(data);
            }
        }

        public void Dispose()
        {
            _serverSocket.Dispose();
        }
    }
}
