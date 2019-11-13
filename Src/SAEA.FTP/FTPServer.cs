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
using SAEA.Sockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.FTP
{
    public class FTPServer : IDisposable
    {
        ServerSocket _cmdSocket;

        IServerSokcet _dataSocket;

        ServerConfig _serverConfig;

        public bool Running { get; set; }

        public event Action<string, Exception> OnLog;

        public FTPServer(string ip, ushort port = 21, int bufferSize = 10240)
        {
            _serverConfig = FTPServerConfigManager.Get();
            _serverConfig.Port = port;
            _serverConfig.BufferSize = bufferSize;
            FTPServerConfigManager.Save();

            _cmdSocket = new ServerSocket(_serverConfig);
            _cmdSocket.OnReceived += _serverSocket_OnReceived;
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
                            _cmdSocket.Reply(id, ServerResponseCode.无效的用户名, "Invalid user name.");
                        }
                        else
                        {
                            if (FTPServerConfigManager.GetUserBind(id) == user.UserName)
                            {
                                if (user.IsLogin == true)
                                    _cmdSocket.Reply(id, ServerResponseCode.初始命令没有执行, "Already logged-in.");
                                else
                                    _cmdSocket.Reply(id, ServerResponseCode.要求密码, "User name okay, need password.");
                            }
                            else
                            {
                                if (user.UserName.ToLower() == "anonymous")
                                {
                                    FTPServerConfigManager.UserBinding(id, user.UserName);
                                    _cmdSocket.Reply(id, ServerResponseCode.登录因特网, "User logged in, proceed.");
                                }
                                else
                                {
                                    FTPServerConfigManager.UserBinding(id, user.UserName);
                                    _cmdSocket.Reply(id, ServerResponseCode.要求密码, "User name okay, need password.");
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
                            _cmdSocket.Reply(id, ServerResponseCode.初始命令没有执行, "Already logged-in.");
                        }
                        else
                        {
                            if (FTPServerConfigManager.UserLogin(userName, cr.Arg))
                            {
                                _cmdSocket.Reply(id, ServerResponseCode.登录因特网, "User logged in, proceed.");
                            }
                            else
                            {
                                _cmdSocket.Reply(id, ServerResponseCode.无效的用户名, "Authentication failed.");
                            }
                        }
                    }
                    else
                    {
                        _cmdSocket.Reply(id, ServerResponseCode.错误指令序列, "Login with USER first.");
                    }
                    return;
                case FTPCommand.SYST:
                    _cmdSocket.Reply(id, ServerResponseCode.系统类型回复, "WINDOWS Type: SAEA FTP Server");
                    return;
                case FTPCommand.NOOP:
                    _cmdSocket.Reply(id, ServerResponseCode.成功, "Command okay.");
                    return;
            }

            if (user == null || !user.IsLogin)
            {
                _cmdSocket.Reply(id, ServerResponseCode.错误指令序列, "Login with USER first.");
                return;
            }

            switch (cmd)
            {
                case FTPCommand.CWD:
                    if (PathHelper.Exists(user.Root, cr.Arg, out string newDirPath))
                    {
                        user.CurrentFtpPath = newDirPath;
                        _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "Command okay.");
                    }
                    else
                    {
                        _cmdSocket.Reply(id, ServerResponseCode.页文件不可用, "No such directory.");
                    }
                    break;
                case FTPCommand.CDUP:

                    if (PathHelper.Exists(user.CurrentFtpPath, "../", out newDirPath))
                    {
                        if (!PathHelper.IsParent(newDirPath, user.Root))
                        {
                            user.CurrentFtpPath = newDirPath;
                            _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "Command okay.");
                            return;
                        }
                    }
                    _cmdSocket.Reply(id, ServerResponseCode.页文件不可用, "No such directory.");
                    break;
                case FTPCommand.PWD:
                    _cmdSocket.Reply(id, ServerResponseCode.路径名建立, $"\"{ user.CurrentFtpPath}\"");
                    break;

                case FTPCommand.PASV:
                    var dataPort = IPHelper.GetFreePort();
                    var portStr = dataPort.PortToString();
                    var pasvStr = $"SAEA FTPServer PASV({_serverConfig.IP},{portStr})";

                    _cmdSocket.CreateDataSocket(userName, dataPort, _serverConfig.BufferSize);

                    _cmdSocket.Reply(id, ServerResponseCode.进入被动模式, pasvStr);

                    break;
                case FTPCommand.MLSD:

                    var path = user.CurrentFtpPath;

                    if (!string.IsNullOrEmpty(cr.Arg) && cr.Arg != "/")
                    {
                        if (PathHelper.Exists(path, cr.Arg, out newDirPath))
                        {
                            path = newDirPath;
                        }
                    }

                    var dirList = PathHelper.GetDirectories(path, out List<FileInfo> fileList);

                    StringBuilder sb = new StringBuilder();

                    if (dirList != null && dirList.Any())
                    {
                        foreach (var item in dirList)
                        {
                            sb.AppendLine($"type=dir;modify={item.LastWriteTime.ToFileTimeUtc()}; {item.Name}");
                        }
                    }

                    if (fileList != null && fileList.Any())
                    {
                        foreach (var item in fileList)
                        {
                            sb.AppendLine($"type=file;modify={item.LastWriteTime.ToFileTimeUtc()}; {item.Name}");
                        }
                    }

                    var str = sb.ToString();

                    if (!string.IsNullOrEmpty(str))
                    {
                        _cmdSocket.SendData(userName, Encoding.UTF8.GetBytes(str));
                    }
                    break;
                case FTPCommand.NLST:

                    path = user.CurrentFtpPath;

                    if (!string.IsNullOrEmpty(cr.Arg) && cr.Arg != "/")
                    {
                        if (PathHelper.Exists(path, cr.Arg, out newDirPath))
                        {
                            path = newDirPath;
                        }
                    }

                    dirList = PathHelper.GetDirectories(path, out fileList);

                    sb = new StringBuilder();

                    if (dirList != null && dirList.Any())
                    {
                        foreach (var item in dirList)
                        {
                            sb.AppendLine($"{item.FullName.Replace(user.CurrentFtpPath, "").Replace("\\", "/")}");
                        }
                    }

                    if (fileList != null && fileList.Any())
                    {
                        foreach (var item in fileList)
                        {
                            sb.AppendLine($"{item.FullName.Replace(user.CurrentFtpPath, "").Replace("\\", "/")}");
                        }
                    }

                    str = sb.ToString();

                    if (!string.IsNullOrEmpty(str))
                    {
                        _cmdSocket.SendData(userName, Encoding.UTF8.GetBytes(str));
                    }
                    break;
            }
        }

        public void Start()
        {
            _cmdSocket.Start();
            this.Running = true;
        }

        public void Stop()
        {
            _cmdSocket.Stop();
            this.Running = false;
        }


        public void Dispose()
        {
            _cmdSocket?.Dispose();
        }
    }
}
