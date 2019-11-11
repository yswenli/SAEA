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
using SAEA.Sockets;
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

        private void _serverSocket_OnError(string ID, Exception ex)
        {
            LogHelper.Error("FTPServer Error", ex);
        }
      
        private void _serverSocket_OnReceive(object currentObj, byte[] data)
        {
            throw new NotImplementedException();
        }
        private void _serverSocket_OnDisconnected(string ID, Exception ex)
        {
            throw new NotImplementedException();
        }


        public void Dispose()
        {
            _serverSocket.Dispose();
        }
    }
}
