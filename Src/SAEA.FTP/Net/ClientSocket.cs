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
using SAEA.FTP.Model;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.FTP.Net
{
    class ClientSocket
    {
        IClientSocket _cmdSocket = null;

        bool _isFirst = true;

        public event OnDisconnectedHandler OnDisconnected;

        FTPStream _ftpStream;

        SyncHelper<ServerResponse> _syncHelper1;

        SyncHelper<ServerResponse> _syncHelper2;

        ClientConfig _config;

        public bool Connected { get; set; } = false;

        public ClientSocket(ClientConfig config)
        {
            _config = config;

            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp()
                .SetIP(_config.IP)
                .SetPort(_config.Port)
                .Build();

            _cmdSocket = SocketFactory.CreateClientSocket(option);
            _cmdSocket.OnError += _clientSocket_OnError;
            _cmdSocket.OnReceive += _clientSocket_OnReceive;
            _cmdSocket.OnDisconnected += _clientSocket_OnDisconnected;

            _ftpStream = new FTPStream();
            _syncHelper1 = new SyncHelper<ServerResponse>();
            _syncHelper2 = new SyncHelper<ServerResponse>();
        }


        private void _clientSocket_OnError(string ID, Exception ex)
        {
            //todo
        }

        private void _clientSocket_OnReceive(byte[] data)
        {
            _ftpStream.Write(data);

            if (_isFirst)
            {
                _syncHelper1.Set(ServerResponse.Parse(_ftpStream.ReadText()));
                _isFirst = false;
                return;
            }
            _syncHelper2.Set(ServerResponse.Parse(_ftpStream.ReadText()));
        }


        private void _clientSocket_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }


        public void Connect()
        {
            if (!Connected)
            {
                ServerResponse result = null;

                _syncHelper1.Wait(() =>
                {
                    _cmdSocket.Connect();
                }, (r) =>
                {
                    result = r;
                });

                if (result.Code != ServerResponseCode.服务就绪)
                {
                    _cmdSocket.Disconnect();
                    throw new Exception(result.Reply);
                }

                result = BaseSend($"{FTPCommand.USER} {_config.UserName}");

                if (result.Code != ServerResponseCode.登录因特网 && result.Code != ServerResponseCode.要求密码)
                {
                    _cmdSocket.Disconnect();
                    throw new Exception(result.Reply);
                }

                if (result.Code == ServerResponseCode.要求密码)
                {
                    result = BaseSend($"{FTPCommand.PASS} {_config.Password}");

                    if (result.Code != ServerResponseCode.登录因特网 && result.Code != ServerResponseCode.初始命令没有执行)
                    {
                        _cmdSocket.Disconnect();
                        throw new Exception(result.Reply);
                    }
                }

                result = BaseSend($"{FTPCommand.SYST}");

                if (result.Code != ServerResponseCode.系统类型回复)
                {
                    _cmdSocket.Disconnect();
                    throw new Exception(result.Reply);
                }

                SetUtf8();

                Connected = true;
            }
        }

        public ServerResponse SetUtf8()
        {
            var result= BaseSend("OPTS UTF8 ON");

            return result;
        }

        public ServerResponse BaseSend(string cmd, Action action = null)
        {
            ServerResponse result = null;
            _syncHelper2.Wait(() =>
            {
                _cmdSocket.SendAsync(Encoding.UTF8.GetBytes(cmd + Environment.NewLine));
                action?.Invoke();
            }, (r) =>
            {
                result = r;
            });
            return result;
        }

        public IClientSocket CreateDataConnection()
        {
            var result = BaseSend($"{FTPCommand.PASV}");

            int num = result.Reply.IndexOf('(');

            int num2 = result.Reply.IndexOf(')');

            string text = result.Reply.Substring(num + 1, num2 - num - 1);

            string[] array = new string[6];

            array = text.Split(new char[]
            {
               ','
            });

            if (array.Length != 6)
            {
                throw new IOException("Malformed PASV strReply: " + result.Reply);
            }

            string ip = string.Concat(new string[]
            {
                array[0],
                ".",
                array[1],
                ".",
                array[2],
                ".",
                array[3]
            });

            try
            {
                num = int.Parse(array[4]);
                num2 = int.Parse(array[5]);
            }
            catch
            {
                throw new IOException("Malformed PASV strReply: " + result.Reply);
            }

            int port = (num << 8) + num2;

            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp()
                .SetIP(ip)
                .SetPort(port)
                .Build();

            var dataSocket = SocketFactory.CreateClientSocket(option);
            dataSocket.OnError += _clientSocket_OnError;
            dataSocket.OnReceive += _dataSocket_OnReceive;
            dataSocket.OnDisconnected += _clientSocket_OnDisconnected;
            dataSocket.Connect();
            return dataSocket;
        }

        private void _dataSocket_OnReceive(byte[] data)
        {
            FTPDataManager.Receive(data);
        }

    }
}
