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
*命名空间：SAEA.FTP.Core
*文件名： FTPDataSocketManager
*版本号： v26.4.23.1
*唯一标识：92ca5dc6-fd30-49ee-826b-c68533703a1d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/13 17:01:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/13 17:01:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
