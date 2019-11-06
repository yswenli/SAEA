/****************************************************************************
*项目名称：SAEA.FTP.Net
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Net
*类 名 称：ClientSocket
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/9/27 17:02:27
*描述：
*=====================================================================
*修改时间：2019/9/27 17:02:27
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Core;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Net
{
    class ClientSocket
    {
        IClientSocket _clientSocket = null;

        bool _isCmd = true;

        public event OnDisconnectedHandler OnDisconnected;

        FTPStream _ftpStream;

        SyncHelper<string> _cmdCache;

        public ClientSocket(ClientConfig config, bool isCmd = true)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp()
                .SetIP(config.IP)
                .SetPort(config.Port)
                .Build();

            _clientSocket = SocketFactory.CreateClientSocket(option);
            _clientSocket.OnError += _clientSocket_OnError;
            _clientSocket.OnReceive += _clientSocket_OnReceive;
            _clientSocket.OnDisconnected += _clientSocket_OnDisconnected;

            _isCmd = isCmd;
            _ftpStream = new FTPStream();
            _cmdCache = new SyncHelper<string>();
        }


        private void _clientSocket_OnError(string ID, Exception ex)
        {
            throw new NotImplementedException();
        }

        private void _clientSocket_OnReceive(byte[] data)
        {
            if (_isCmd)
            {
                _ftpStream.Write(data);
                _ftpStream.ReadLine();
            }
            else
            {

            }
        }
        private void _clientSocket_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }

        public void Connect()
        {
            _clientSocket.Connect();
        }

        public void Request(byte[] data)
        {
            _cmdCache.TryAdd()
            _clientSocket.SendAsync(data);

        }
    }
}
