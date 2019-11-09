/****************************************************************************
*项目名称：SAEA.FTP
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP
*类 名 称：FTPClient
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/9/27 15:08:55
*描述：
*=====================================================================
*修改时间：2019/9/27 15:08:55
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
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.FTP
{
    public class FTPClient
    {
        ClientSocket _client;

        public bool Connected
        {
            get
            {
                return _client.Connected;
            }
        }

        public FTPClient(ClientConfig config)
        {
            _client = new ClientSocket(config);
        }

        public FTPClient(string ip, int port, string userName, string password) : this(new ClientConfig() { IP = ip, Port = port, UserName = userName, Password = password })
        {

        }

        public void Connect()
        {
            _client.Connect();
        }

        public void Noop()
        {
            var sres = _client.BaseSend($"{FTPCommand.NOOP}");

            if (sres.Code != ServerResponseCode.成功)
            {
                throw new Exception($"code:{sres.Code},reply:{sres.Reply}");
            }
        }

        /// <summary>
        /// 更改工作目录
        /// </summary>
        /// <param name="pathName"></param>
        /// <returns></returns>
        public bool ChangeDir(string pathName)
        {
            var sres = _client.BaseSend($"{FTPCommand.CWD} {pathName}");

            if (sres.Code == ServerResponseCode.文件行为完成)
            {
                return true;
            }
            if (sres.Code == ServerResponseCode.页文件不可用)
            {
                return false;
            }
            throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
        }
        /// <summary>
        /// 更改工作目录到父目录
        /// </summary>
        /// <returns></returns>
        public bool ChangeToParentDir()
        {
            var sres = _client.BaseSend($"{FTPCommand.CDUP}");

            if (sres.Code == ServerResponseCode.文件行为完成 || sres.Code == ServerResponseCode.成功)
            {
                return true;
            }
            if (sres.Code == ServerResponseCode.页文件不可用)
            {
                return false;
            }
            throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
        }
        /// <summary>
        /// 返回当前工作目录目录
        /// </summary>
        /// <returns></returns>
        public string CurrentDir()
        {
            var sres = _client.BaseSend($"{FTPCommand.PWD}");

            if (sres.Code == ServerResponseCode.路径名建立)
            {
                var dir = sres.Reply;

                dir = dir.Substring(dir.IndexOf("\"") + 1);

                dir = dir.Substring(0, dir.IndexOf("\""));

                return dir;
            }
            throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
        }

        /// <summary>
        /// 功能：返回指定路径下的子目录及文件列表，默认为当前工作地址
        /// </summary>
        /// <param name="pathName"></param>
        /// <param name="dirType"></param>
        /// <returns></returns>
        public List<string> Dir(string pathName = "/", DirType dirType = DirType.LIST)
        {
            using (var dataSocket = _client.CreateDataConnection())
            {
                FTPDataManager.New();

                var sres = _client.BaseSend($"{dirType.ToString()} {pathName}");

                var str = FTPDataManager.ReadText();

                if (string.IsNullOrEmpty(str))
                {
                    if (ChangeDir(pathName))
                    {
                        return new List<string>();
                    }
                    else
                    {
                        return null;
                    }
                }
                return str.Split(Environment.NewLine).ToList();
            }
        }

        public void MakeDir(string pathName)
        {
            var sres = _client.BaseSend($"{FTPCommand.MKD} {pathName}");

            if (sres.Code != ServerResponseCode.文件行为完成)
            {
                throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
            }
        }

        public void RemoveDir(string pathName)
        {
            var sres = _client.BaseSend($"{FTPCommand.RMD} {pathName}");

            if (sres.Code != ServerResponseCode.文件行为完成)
            {
                throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
            }
        }


        public void Rename(string oldName, string newName)
        {
            _client.BaseSend($"{FTPCommand.RNFR} {oldName}");

            var sres = _client.BaseSend($"{FTPCommand.RNTO} {newName}");

            if (sres.Code != ServerResponseCode.文件行为完成)
            {
                throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
            }
        }

        public void Delete(string fileName)
        {
            var sres = _client.BaseSend($"{FTPCommand.DELE} {fileName}");

            if (sres.Code != ServerResponseCode.文件行为完成)
            {
                throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
            }
        }


        public void Upload(string filePath)
        {
            using (var dataSocket = _client.CreateDataConnection())
            {
                var fileName = PathHelper.GetFileName(filePath);

                var sres = _client.BaseSend($"{FTPCommand.STOR} {fileName}");

                if (sres.Code != ServerResponseCode.打开数据连接开始传输 && sres.Code != ServerResponseCode.打开连接)
                {
                    throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
                }

                using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] data = new byte[1024];

                    int numBytesRead = 0;

                    while (true)
                    {
                        int n = fs.Read(data, 0, 1024);

                        if (n == 0)
                            break;

                        numBytesRead += n;

                        if (n == 1024)
                        {
                            dataSocket.Send(data);
                        }
                        else
                        {
                            dataSocket.Send(data.AsSpan().Slice(0, n).ToArray());
                        }
                    }
                }
            }
        }

        public string Download(string fileName, string filePath, int timeOut = 3 * 1000)
        {
            using (var dataSocket = _client.CreateDataConnection())
            {
                FTPDataManager.New(filePath);

                var sres = _client.BaseSend($"{FTPCommand.RETR} {fileName}");

                if (sres.Code == ServerResponseCode.结束数据连接 || sres.Code == ServerResponseCode.打开连接)
                {
                    if (FTPDataManager.Checked(timeOut))
                    {
                        return filePath;
                    }
                    return string.Empty;
                }
                else
                {
                    throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
                }
            }
        }


        public long FileSize(string fileName)
        {
            var sres = _client.BaseSend($"{FTPCommand.SIZE} {fileName}");

            if (sres.Code == ServerResponseCode.文件状态回复)
            {
                return long.Parse(sres.Reply);
            }
            else
            {
                throw new IOException($"code:{sres.Code},reply:{sres.Reply}");
            }
        }

        public void Quit()
        {
            var sres = _client.BaseSend($"{FTPCommand.QUIT}");

            if (sres.Code != ServerResponseCode.成功)
            {
                throw new Exception($"code:{sres.Code},reply:{sres.Reply}");
            }
        }
    }
}
