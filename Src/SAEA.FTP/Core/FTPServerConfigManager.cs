/****************************************************************************
*项目名称：SAEA.FTP.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Core
*类 名 称：FTPUserManager
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 16:09:36
*描述：
*=====================================================================
*修改时间：2019/11/11 16:09:36
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Core
{
    public static class FTPServerConfigManager
    {
        static ServerConfig _serverConfig = null;

        static ConfigHelper<ServerConfig> _configHelper;

        static FTPServerConfigManager()
        {
            _configHelper = new ConfigHelper<ServerConfig>("Configs", "FTPServer.Config");
        }

        public static ServerConfig Get()
        {
            var sc = _configHelper.Read();

            if (sc == null)
            {
                sc = new ServerConfig();
            }

            _serverConfig = sc;

            return sc;
        }

        public static void Set(ServerConfig serverConfig)
        {
            _configHelper.Write(serverConfig);
        }

        public static void Save()
        {

        }
    }
}
