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
*命名空间：SAEA.FTP.Net
*文件名： ServerSocket
*版本号： v26.4.23.1
*唯一标识：ed092dd9-bbf8-461f-8c4a-37b4bd1bbd24
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 15:49:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/11 15:49:32
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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

        private void _serverSocket_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnDisconnected?.Invoke(id);
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
