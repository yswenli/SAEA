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
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;
using System.Text;

namespace SAEA.FTP.Net
{
    class ServerSocket : IDisposable
    {
        IServerSocket _serverSocket;

        ServerConfig _serverConfig;

        FTPStream _ftpStream;

        public event Action<string, string> OnReceived;

        public event Action<string> OnDisconnected;

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

            _serverSocket.OnAccepted += _serverSocket_OnAccepted;

            _serverSocket.OnReceive += _serverSocket_OnReceive;

            _serverSocket.OnDisconnected += _serverSocket_OnDisconnected;

            _ftpStream = new FTPStream();
        }

        private void _serverSocket_OnAccepted(object obj)
        {
            var ut = obj as IUserToken;

            var data = Encoding.UTF8.GetBytes($"{ServerResponseCode.服务就绪} Welcome to SAEA.FTPServer! {DateTimeHelper.GetUnixTick()}{Environment.NewLine}");

            _serverSocket.SendAsync(ut.ID, data);
        }

        public void Start()
        {
            _serverSocket.Start();
        }

        public void Reply(string id, int code, string msg)
        {
            var data = Encoding.UTF8.GetBytes($"{code} {msg}{Environment.NewLine}");

            _serverSocket.SendAsync(id, data);
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
                var ut = currentObj as IUserToken;
                OnReceived?.Invoke(ut.ID, msg);
            }
        }

        private void _serverSocket_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID);
        }



        #region PASV

        public void CreateDataSocket(string userName, ushort port, int bufferSize = 10240)
        {
            FTPServerConfigManager.GetUser(userName).FTPDataSocketManager = new FTPDataSocketManager(userName, port, bufferSize);
        }


        public void SendData(string userName, byte[] data)
        {
            var ftpUser = FTPServerConfigManager.GetUser(userName);

            using (var ftpDataSocketManager = ftpUser.FTPDataSocketManager)
            {
                ftpUser.FTPDataSocketManager.SendData(data);
            }
        }

        public void SendFile(string userName, string filePath)
        {
            var ftpUser = FTPServerConfigManager.GetUser(userName);

            using (var ftpDataSocketManager = ftpUser.FTPDataSocketManager)
            {
                ftpUser.FTPDataSocketManager.SendFile(filePath);
            }
        }

        public void ReceiveFile(string userName, string filePath)
        {
            var ftpUser = FTPServerConfigManager.GetUser(userName);

            using (var ftpDataSocketManager = ftpUser.FTPDataSocketManager)
            {
                ftpUser.FTPDataManager.New(filePath);

                ftpDataSocketManager.Checke();
            }
        }

        #endregion



        public void Dispose()
        {
            _serverSocket.Dispose();
        }
    }
}
