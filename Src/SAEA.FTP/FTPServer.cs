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
using SAEA.Common;
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

            var userName = FTPServerConfigManager.GetUserBind(id);

            var user = FTPServerConfigManager.GetUser(userName);

            switch (cmd)
            {
                case FTPCommand.USER:
                    if (!string.IsNullOrEmpty(cr.Arg))
                    {
                        if (user == null)
                        {
                            _serverSocket.Reply(id, ServerResponseCode.无效的用户名, "Invalid user name.");
                        }
                        else
                        {
                            if (FTPServerConfigManager.GetUserBind(id) == user.UserName)
                            {
                                if (user.IsLogin == true)
                                    _serverSocket.Reply(id, ServerResponseCode.初始命令没有执行, "Already logged-in.");
                                else
                                    _serverSocket.Reply(id, ServerResponseCode.要求密码, "User name okay, need password.");
                            }
                            else
                            {
                                if (user.UserName.ToLower() == "anonymous")
                                {
                                    FTPServerConfigManager.UserBinding(id, user.UserName);
                                    _serverSocket.Reply(id, ServerResponseCode.登录因特网, "User logged in, proceed.");
                                }
                                else
                                {
                                    FTPServerConfigManager.UserBinding(id, user.UserName);
                                    _serverSocket.Reply(id, ServerResponseCode.要求密码, "User name okay, need password.");
                                }
                            }
                        }
                    }
                    return;
                case FTPCommand.PASS:
                    if (!string.IsNullOrEmpty(userName))
                    {
                        if (user.IsLogin)
                        {
                            _serverSocket.Reply(id, ServerResponseCode.初始命令没有执行, "Already logged-in.");
                        }
                        else
                        {
                            if (FTPServerConfigManager.UserLogin(userName, cr.Arg))
                            {
                                _serverSocket.Reply(id, ServerResponseCode.登录因特网, "User logged in, proceed.");
                            }
                            else
                            {
                                _serverSocket.Reply(id, ServerResponseCode.无效的用户名, "Authentication failed.");
                            }
                        }
                    }
                    else
                    {
                        _serverSocket.Reply(id, ServerResponseCode.错误指令序列, "Login with USER first.");
                    }
                    return;
                case FTPCommand.SYST:
                    _serverSocket.Reply(id, ServerResponseCode.系统类型回复, "WINDOWS Type: SAEA FTP Server");
                    return;
                case FTPCommand.NOOP:
                    _serverSocket.Reply(id, ServerResponseCode.成功, "Command okay.");
                    return;
            }

            if (user == null || !user.IsLogin)
            {
                _serverSocket.Reply(id, ServerResponseCode.错误指令序列, "Login with USER first.");
                return;
            }

            switch (cmd)
            {
                case FTPCommand.CWD:
                    if (PathHelper.Exists(user.Root, cr.Arg, out string newDirPath))
                    {
                        user.CurrentFtpPath = newDirPath;
                        _serverSocket.Reply(id, ServerResponseCode.文件行为完成, "Command okay.");
                    }
                    else
                    {
                        _serverSocket.Reply(id, ServerResponseCode.页文件不可用, "No such directory.");
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
