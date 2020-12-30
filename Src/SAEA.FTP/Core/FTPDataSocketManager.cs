/****************************************************************************
*项目名称：SAEA.FTP.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Core
*类 名 称：FTPDataSocketManager
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/13 16:13:08
*描述：
*=====================================================================
*修改时间：2019/11/13 16:13:08
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;
using System.Threading;

namespace SAEA.FTP.Core
{
    public class FTPDataSocketManager : IDisposable
    {
        AutoResetEvent _autoResetEvent;

        IServerSocket _dataSocket;

        string _userName = string.Empty;

        IUserToken _userToken = null;

        public bool IsConnected = false;

        public FTPDataSocketManager(string userName, ushort port, int bufferSize = 10240)
        {
            _autoResetEvent = new AutoResetEvent(false);

            var option = SocketOptionBuilder.Instance
              .SetSocket()
              .UseIocp()
              .SetPort(port)
              .SetReadBufferSize(bufferSize)
              .SetWriteBufferSize(bufferSize)
              .Build();
            var dataSocket = SocketFactory.CreateServerSocket(option);
            dataSocket.OnAccepted += DataSocket_OnAccepted;
            dataSocket.OnDisconnected += DataSocket_OnDisconnected;
            dataSocket.OnError += _serverSocket_OnError;
            dataSocket.OnReceive += DataSocket_OnReceive;
            _dataSocket = dataSocket;
            _userName = userName;
            _dataSocket.Start();
        }

        private void DataSocket_OnDisconnected(string ID, Exception ex)
        {
            IsConnected = false;
        }

        private void DataSocket_OnAccepted(object obj)
        {
            _userToken = obj as IUserToken;

            IsConnected = true;
        }


        private void DataSocket_OnReceive(object currentObj, byte[] data)
        {
            var ftpUser = FTPServerConfigManager.GetUser(_userName);
            ftpUser.FTPDataManager.Receive(data);
        }


        public void SendData(byte[] data)
        {
            while (!IsConnected)
            {
                _autoResetEvent.WaitOne(10);
            }
            _dataSocket.End(_userToken.ID, data);
        }

        public void SendFile(string filePath)
        {
            while (!IsConnected)
            {
                _autoResetEvent.WaitOne(10);
            }
            FileHelper.Read(filePath, (data) =>
            {
                _dataSocket.Send(_userToken.ID, data);
            });
            while (IsConnected)
            {
                _autoResetEvent.WaitOne(10);
            }
        }

        public void Checke()
        {
            var ftpUser = FTPServerConfigManager.GetUser(_userName);

            while (IsConnected)
            {
                _autoResetEvent.WaitOne(1);

                if (!IsConnected)
                {
                    break;
                }
            }
            ftpUser.FTPDataManager.FileComplete();
        }


        private void _serverSocket_OnError(string ID, Exception ex)
        {
            LogHelper.Error("FTPDataSocketManager Error", ex);
        }

        public void Dispose()
        {
            _dataSocket?.Dispose();
            _dataSocket = null;
        }
    }
}
