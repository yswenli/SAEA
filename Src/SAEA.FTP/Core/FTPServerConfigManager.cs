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
*文件名： FTPServerConfigManager
*版本号： v26.4.23.1
*唯一标识：5992fd08-1337-4856-9ff6-eca2a0ef2134
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 17:28:07
*描述：FTPServerConfigManager接口
*
*=====================================================================
*修改标记
*修改时间：2019/11/11 17:28:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：FTPServerConfigManager接口
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Model;
using System.Collections.Concurrent;

namespace SAEA.FTP.Core
{
    public static class FTPServerConfigManager
    {
        static ServerConfig _serverConfig = null;

        static ConfigHelper<ServerConfig> _configHelper;

        static ConcurrentDictionary<string, string> _userBinds;

        static FTPServerConfigManager()
        {
            _configHelper = new ConfigHelper<ServerConfig>("Configs", "FTPServer.Config");

            Get();

            _userBinds = new ConcurrentDictionary<string, string>();
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

        public static void Set(ushort port, int bufferSize = 10240)
        {
            _serverConfig.Port = port;
            _serverConfig.BufferSize = 10240;
        }

        public static void Save()
        {
            _configHelper.Write(_serverConfig);
        }

        public static void SetUser(string userName, string password, string root)
        {
            if (_serverConfig.Users == null)
            {
                _serverConfig.Users = new ConcurrentDictionary<string, FTPUser>();
            }

            var user = new FTPUser(userName, password, root);

            _serverConfig.Users.AddOrUpdate(userName, user, (k, v) =>
            {
                return user;
            });
        }

        public static FTPUser GetUser(string userName)
        {
            if (_serverConfig != null && _serverConfig.Users != null)
            {
                if (_serverConfig.Users.TryGetValue(userName, out FTPUser user))
                {
                    if (user.FTPDataManager == null)
                    {
                        user.FTPDataManager = new FTPDataManager();
                    }

                    return user;
                }
            }
            return null;
        }

        public static bool UserLogin(string userName, string password)
        {
            if (_serverConfig != null && _serverConfig.Users != null)
            {
                if (_serverConfig.Users.TryGetValue(userName, out FTPUser user))
                {
                    if (userName.ToLower() == "anonymous")
                    {
                        user.IsLogin = true;
                        return true;
                    }
                    else
                    {
                        if (user.Password == password)
                        {
                            user.IsLogin = true;
                            return true;
                        }
                    }

                }
            }
            return false;
        }

        public static void DelUser(string userName)
        {
            if (_serverConfig != null && _serverConfig.Users != null)
            {
                _serverConfig.Users.TryRemove(userName, out FTPUser user);
            }
        }

        #region user binding

        public static void UserBinding(string id, string userName)
        {
            _userBinds.AddOrUpdate(id, userName, (k, v) => userName);
        }

        public static string GetUserBind(string id)
        {
            if (_userBinds.TryGetValue(id, out string userName))
            {
                return userName;
            }
            return string.Empty;
        }

        public static void RemoveUserBind(string id)
        {
            _userBinds.TryRemove(id, out string userName);
        }

        #endregion
    }
}
