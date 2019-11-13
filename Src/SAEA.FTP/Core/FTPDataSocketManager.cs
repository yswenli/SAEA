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
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SAEA.FTP.Core
{
    public class FTPDataSocketManager : IDisposable
    {
        AutoResetEvent _autoResetEvent1;

        AutoResetEvent _autoResetEvent2;

        IServerSokcet _dataSocket;

        string _userName = string.Empty;

        string _id = string.Empty;

        public FTPDataSocketManager(string userName, ushort port, int bufferSize = 10240)
        {
            _autoResetEvent1 = new AutoResetEvent(false);
            _autoResetEvent2 = new AutoResetEvent(false);


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
        }

        private void DataSocket_OnDisconnected(string ID, Exception ex)
        {
            _autoResetEvent2.Set();
        }

        private void DataSocket_OnAccepted(object obj)
        {
            var ut = obj as IUserToken;
            _id = ut.ID;
            _autoResetEvent1.Set();
        }

        private void DataSocket_OnReceive(object currentObj, byte[] data)
        {
            var ut = currentObj as IUserToken;
            var ftpUser = FTPServerConfigManager.GetUser(_userName);
            ftpUser.FTPDataManager.Receive(data);
        }


        public void SendData(byte[] data)
        {
            _dataSocket.Start();
            _autoResetEvent1.WaitOne();
            _dataSocket.SendAsync(_id, data);
        }

        public void SendFile(string filePath)
        {
            _dataSocket.Start();
            _autoResetEvent1.WaitOne();
            FileHelper.Read(filePath, (data) =>
            {
                _dataSocket.SendAsync(_id, data);
            });
        }

        public void Checke()
        {
            _autoResetEvent2.WaitOne();
        }


        private void _serverSocket_OnError(string ID, Exception ex)
        {
            LogHelper.Error("FTPDataSocketManager Error", ex);
        }

        public void Dispose()
        {
            _dataSocket?.Dispose();
        }
    }
}
