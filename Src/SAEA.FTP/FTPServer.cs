/****************************************************************************
*项目名称：SAEA.FTP
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP
*类 名 称：FTPServer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/9/27 15:08:45
*描述：
*=====================================================================
*修改时间：2019/9/27 15:08:45
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP
{
    public class FTPServer : IDisposable
    {
        ServerSocket _serverSocket;

        ServerConfig _serverConfig;

        public bool Running { get; set; }

        public event Action<string, Exception> OnLog;

        public FTPServer(int port = 21, int bufferSize = 10240)
        {
            _serverConfig = FTPServerConfigManager.Get();
            _serverConfig.Port = 21;
            _serverConfig.BufferSize = 10240;
            FTPServerConfigManager.Save();

            _serverSocket = new ServerSocket(_serverConfig);
            _serverSocket.OnReceived += _serverSocket_OnReceived;
        }

        private void _serverSocket_OnReceived(string id, string msg)
        {
            var cr = ClientRequest.Parse(msg);

            var cmd = Enum.Parse(typeof(FTPCommand), cr.Cmd);

            switch (cmd)
            {
                case FTPCommand.USER:
                    if (!string.IsNullOrEmpty(cr.Arg))
                    {
                        var user = FTPServerConfigManager.GetUser(cr.Arg);
                        if (user == null)
                        {
                            _serverSocket.Reply(id, ServerResponseCode.无效的用户名, "Invalid user name");
                        }
                    }
                    break;
            }
        }

        public void Start()
        {
            _serverSocket.Start();
            this.Running = true;
        }

        public void Stop()
        {
            _serverSocket.Stop();
            this.Running = false;
        }


        public void Dispose()
        {
            _serverSocket?.Dispose();
        }
    }
}
