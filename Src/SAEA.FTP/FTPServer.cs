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
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
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
            _cmdSocket.OnDisconnected += _cmdSocket_OnDisconnected;
        }



        private void _serverSocket_OnReceived(string id, string msg)
        {
            try
            {
                OnLog($"收到命令：{msg.Trim()}", null);

                var cr = ClientRequest.Parse(msg);

                var cmd = Enum.Parse(typeof(FTPCommand), cr.Cmd);

                var userName = FTPServerConfigManager.GetUserBind(id);

                var user = FTPServerConfigManager.GetUser(userName);

                switch (cmd)
                {
                    case FTPCommand.USER:
                        if (!string.IsNullOrEmpty(cr.Arg))
                        {
                            userName = cr.Arg;

                            user = FTPServerConfigManager.GetUser(userName);

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
                                        _cmdSocket.Reply(id, ServerResponseCode.登录成功, "User logged in, proceed.");
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
                                    _cmdSocket.Reply(id, ServerResponseCode.登录成功, "User logged in, proceed.");
                                    OnLog($"{userName}登录成功", null);
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
                    case FTPCommand.OPTS:                    
                        _cmdSocket.Reply(id, ServerResponseCode.成功, "WINDOWS Type: SAEA FTP Server");
                        break;
                    case FTPCommand.FEAT:
                        _cmdSocket.Reply(id, ServerResponseCode.目录状态回复, "Extension supported\r\nUTF8");
                        break;
                    case FTPCommand.TYPE:
                        _cmdSocket.Reply(id, ServerResponseCode.成功, "Type set to A");

                        break;
                    case FTPCommand.NOOP:
                        _cmdSocket.Reply(id, ServerResponseCode.成功, "Command okay.");
                        return;
                    case FTPCommand.QUIT:
                        _cmdSocket.Reply(id, ServerResponseCode.退出网络, "SAEA FTP: Goodbye.");
                        OnLog($"{userName}退出网络", null);
                        return;
                }

                if (user == null || !user.IsLogin)
                {
                    _cmdSocket.Reply(id, ServerResponseCode.错误指令序列, "Login with USER first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(user.CurrentFtpPath) || user.CurrentFtpPath == "/" || user.CurrentFtpPath == "\\")
                {
                    user.CurrentPath = user.Root;
                    user.CurrentFtpPath = "/";
                }

                switch (cmd)
                {
                    case FTPCommand.CWD:
                        if (PathHelper.Combine(user.CurrentPath, cr.Arg, out string newDirPath))
                        {
                            user.CurrentFtpPath += cr.Arg + "/";
                            user.CurrentFtpPath = user.CurrentFtpPath.Replace("//", "/");

                            user.CurrentPath = newDirPath;
                            _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "Command okay.");
                        }
                        else
                        {
                            _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "No such directory.");
                        }
                        break;
                    case FTPCommand.CDUP:

                        var parentDir = PathHelper.GetParent(user.CurrentPath);

                        if (parentDir != null)
                        {
                            if (!PathHelper.IsParent(parentDir.ToString(), user.Root))
                            {
                                var cstr = user.CurrentFtpPath.Substring(0, user.CurrentFtpPath.LastIndexOf("/"));
                                user.CurrentFtpPath = cstr.Substring(0, cstr.LastIndexOf("/") + 1);
                                user.CurrentFtpPath = user.CurrentFtpPath.Replace("//", "/");

                                user.CurrentPath = parentDir.ToString();
                            }
                            _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "Command okay.");
                            return;
                        }
                        _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "No such directory.");
                        break;
                    case FTPCommand.PWD:
                        _cmdSocket.Reply(id, ServerResponseCode.路径名建立, $"\"{ user.CurrentFtpPath}\"");
                        break;

                    case FTPCommand.PASV:
                        var dataPort = IPHelper.GetFreePort();
                        var portStr = dataPort.PortToString();
                        var pasvStr = $"SAEA FTPServer PASV({_serverConfig.IP.Replace(".", ",")},{portStr})";

                        _cmdSocket.CreateDataSocket(userName, dataPort, _serverConfig.BufferSize);

                        OnLog($"{userName}进入被动模式，已创建数据传输Socket:{_serverConfig.IP}:{dataPort}", null);

                        _cmdSocket.Reply(id, ServerResponseCode.进入被动模式, pasvStr);

                        break;
                    case FTPCommand.PORT:
                        _cmdSocket.Reply(id, ServerResponseCode.禁用, "Port is disabled.");
                        break;
                    case FTPCommand.MLSD:

                        if (!string.IsNullOrEmpty(cr.Arg) && cr.Arg != "/")
                        {
                            PathHelper.Combine(user.CurrentPath, cr.Arg, out newDirPath);

                            if (string.IsNullOrEmpty(newDirPath))
                            {
                                _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "No such directory.");
                                return;
                            }
                        }

                        var dirList = PathHelper.GetDirectories(user.CurrentPath, out List<FileInfo> fileList);

                        StringBuilder sb = new StringBuilder();

                        if (dirList != null && dirList.Any())
                        {
                            foreach (var item in dirList)
                            {
                                if (item == dirList.Last())
                                    sb.Append($"type=dir;modify={item.LastWriteTime.ToFileTimeUtc()}; {item.Name}");
                                else
                                    sb.AppendLine($"type=dir;modify={item.LastWriteTime.ToFileTimeUtc()}; {item.Name}");
                            }
                        }

                        if (fileList != null && fileList.Any())
                        {
                            if (sb.Length > 0)
                            {
                                sb.Append(Environment.NewLine);
                            }

                            foreach (var item in fileList)
                            {
                                if (item == fileList.Last())
                                    sb.Append($"type=file;modify={item.LastWriteTime.ToFileTimeUtc()};size={item.Length}; {item.Name}");
                                else
                                    sb.AppendLine($"type=file;modify={item.LastWriteTime.ToFileTimeUtc()};size={item.Length}; {item.Name}");
                            }
                        }

                        var str = sb.ToString();

                        _cmdSocket.SendData(userName, Encoding.UTF8.GetBytes(str));
                        OnLog($"已发送数据到{userName}", null);

                        _cmdSocket.Reply(id, ServerResponseCode.打开连接, "File status okay; about to open data connection.");
                        break;
                    case FTPCommand.NLST:

                        if (!string.IsNullOrEmpty(cr.Arg) && cr.Arg != "/")
                        {
                            PathHelper.Combine(user.CurrentPath, cr.Arg, out newDirPath);

                            if (string.IsNullOrEmpty(newDirPath))
                            {
                                _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "No such directory.");
                                return;
                            }
                        }

                        dirList = PathHelper.GetDirectories(user.CurrentPath, out fileList);

                        sb = new StringBuilder();

                        if (dirList != null && dirList.Any())
                        {
                            foreach (var item in dirList)
                            {
                                if (item == dirList.Last())
                                    sb.Append($"{item.FullName.Replace(user.CurrentPath, "").Replace("\\", "/")}");
                                else
                                    sb.AppendLine($"{item.FullName.Replace(user.CurrentPath, "").Replace("\\", "/")}");
                            }
                        }

                        if (fileList != null && fileList.Any())
                        {
                            if (sb.Length > 0)
                            {
                                sb.Append(Environment.NewLine);
                            }
                            foreach (var item in fileList)
                            {
                                if (item == fileList.Last())
                                    sb.Append($"{item.FullName.Replace(user.CurrentPath, "").Replace("\\", "/")}");
                                else
                                    sb.AppendLine($"{item.FullName.Replace(user.CurrentPath, "").Replace("\\", "/")}");
                            }
                        }

                        str = sb.ToString();

                        if (!string.IsNullOrEmpty(str))
                        {
                            _cmdSocket.SendData(userName, Encoding.UTF8.GetBytes(str));
                            OnLog($"已发送数据到{userName}", null);
                        }
                        _cmdSocket.Reply(id, ServerResponseCode.打开连接, "File status okay; about to open data connection.");
                        break;
                    case FTPCommand.SIZE:
                        var fileName = cr.Arg;

                        var filePath = PathHelper.Combine(user.CurrentPath, fileName);

                        var fileInfo = FileHelper.GetFileInfo(filePath);

                        if (fileInfo != null)
                        {
                            _cmdSocket.Reply(id, ServerResponseCode.文件状态回复, fileInfo.Length.ToString());
                        }
                        else
                        {
                            _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "No such file.");
                        }
                        break;
                    case FTPCommand.RETR:

                        fileName = cr.Arg;

                        filePath = PathHelper.Combine(user.CurrentPath, fileName);

                        fileInfo = FileHelper.GetFileInfo(filePath);

                        if (fileInfo != null)
                        {
                            _cmdSocket.Reply(id, ServerResponseCode.打开连接, fileInfo.Length.ToString());

                            _cmdSocket.SendFile(userName, filePath);

                            OnLog($"已发送文件{fileName}到{userName}", null);
                        }
                        else
                        {
                            _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "No such file.");
                        }
                        break;
                    case FTPCommand.STOR:
                        try
                        {
                            fileName = cr.Arg;

                            filePath = PathHelper.Combine(user.CurrentPath, fileName);

                            if (!string.IsNullOrEmpty(filePath))
                            {
                                _cmdSocket.Reply(id, ServerResponseCode.打开连接, "Opening data connection.");

                                _cmdSocket.ReceiveFile(userName, filePath);

                                OnLog($"已从{userName}接收到文件{fileName}", null);
                            }
                            else
                            {
                                _cmdSocket.Reply(id, ServerResponseCode.输出文件出错, "Error on output file.");
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnLog($"从{userName}接收文件时出现异常", ex);
                        }
                        break;
                    case FTPCommand.MKD:
                        try
                        {
                            fileName = cr.Arg;
                            filePath = PathHelper.Combine(user.CurrentPath, fileName);
                            PathHelper.CreateDir(filePath);
                            _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "Directory created.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnLog($"{userName}创建目录{cr.Arg}时出现异常", ex);
                        }
                        _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "Cannot create directory.");
                        break;
                    case FTPCommand.RMD:
                        try
                        {
                            fileName = cr.Arg;
                            filePath = PathHelper.Combine(user.CurrentPath, fileName);
                            PathHelper.Remove(filePath);
                            _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "Directory removed.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnLog($"{userName}移除目录{cr.Arg}时出现异常", ex);
                        }
                        _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "Cannot remove directory.");
                        break;
                    case FTPCommand.DELE:
                        try
                        {
                            fileName = cr.Arg;
                            filePath = PathHelper.Combine(user.CurrentPath, fileName);
                            FileHelper.Remove(filePath);
                            _cmdSocket.Reply(id, ServerResponseCode.文件行为完成, "File removed.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnLog($"{userName}移除文件{cr.Arg}时出现异常", ex);
                        }
                        _cmdSocket.Reply(id, ServerResponseCode.找不到文件或文件夹, "Cannot remove File.");
                        break;
                }
            }
            catch (Exception ex)
            {
                OnLog("FTPServer Error OnReceive 未知的命令", ex);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="id"></param>
        private void _cmdSocket_OnDisconnected(string id)
        {
            var userName = FTPServerConfigManager.GetUserBind(id);

            if (string.IsNullOrEmpty(userName)) return;

            var user = FTPServerConfigManager.GetUser(userName);

            if (user == null) return;

            user.CurrentFtpPath = user.Root;
            user.CurrentPath = "/";
            user.IsLogin = false;
            FTPServerConfigManager.RemoveUserBind(id);
            FTPServerConfigManager.Save();

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
