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
*唯一标识：7bfc806f-f0dc-47e1-a5f9-fc08ba855fa2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/13 17:01:50
*描述：FTPDataSocketManager管理类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <summary>
    /// 服务端为单个用户建立的数据端口监听与数据传输管理
    /// </summary>
    public class FTPDataSocketManager : IDisposable
    {
        AutoResetEvent _autoResetEvent;

        IServerSocket _dataSocket;

        string _userName = string.Empty;

        IUserToken _userToken = null;

        /// <summary>
        /// 数据连接是否已建立；收发前会自旋等待该标志
        /// </summary>
        public bool IsConnected = false;

        /// <summary>
        /// 在指定端口启动数据连接监听
        /// </summary>
        /// <param name="userName">用户名称，用于回查该用户的数据缓冲</param>
        /// <param name="port">数据连接监听端口</param>
        /// <param name="bufferSize">读写缓冲区大小，单位字节</param>
        public FTPDataSocketManager(string userName, ushort port, int bufferSize = 10240)
        {
            _autoResetEvent = new AutoResetEvent(false);

            var option = SocketOptionBuilder.Instance
              .SetSocket()
              .UsePacket()
              .SetPort(port)
              .SetReadBufferSize(bufferSize)
              .SetWriteBufferSize(bufferSize)
              .Build();
            var dataSocket = SocketFactory.CreateServerSocket(option);
            dataSocket.OnAccepted += DataSocket_OnAccepted;
            dataSocket.OnDisconnected += DataSocket_OnDisconnected;
            dataSocket.OnError += _serverSocket_OnError;
            dataSocket.OnServerReceiveSpan += DataSocket_OnReceiveSpan;
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


        private void DataSocket_OnReceiveSpan(IUserToken currentObj, ReadOnlySpan<byte> dataSpan)
        {
            var ftpUser = FTPServerConfigManager.GetUser(_userName);
            ftpUser.FTPDataManager.Receive(dataSpan.ToArray());
        }


        /// <summary>
        /// 等待数据连接建立后发送一段数据，无超时限制
        /// </summary>
        /// <param name="data">要发送的数据</param>
        public void SendData(byte[] data)
        {
            while (!IsConnected)
            {
                _autoResetEvent.WaitOne(10);
            }
            _dataSocket.End(_userToken.ID, data.AsMemory());
        }

        /// <summary>
        /// 等待数据连接建立后按块发送本地文件，直到连接断开才返回
        /// </summary>
        /// <param name="filePath">本地文件路径</param>
        public void SendFile(string filePath)
        {
            while (!IsConnected)
            {
                _autoResetEvent.WaitOne(10);
            }
            FileHelper.Read(filePath, (data) =>
            {
                _dataSocket.Send(_userToken.ID, data.AsSpan());
            });
            while (IsConnected)
            {
                _autoResetEvent.WaitOne(10);
            }
        }

        /// <summary>
        /// 阻塞等待数据连接断开，随后把接收临时文件移动到目标路径
        /// </summary>
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

        /// <summary>
        /// 关闭并释放数据端口监听，可重复调用
        /// </summary>
        public void Dispose()
        {
            _dataSocket?.Dispose();
            _dataSocket = null;
        }
    }
}
