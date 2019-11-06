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
using SAEA.FTP.Net;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP
{
    public class FTPClient
    {
        ClientSocket _client;



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



    }
}
