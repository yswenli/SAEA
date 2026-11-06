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
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Model;
using System.Collections.Concurrent;

namespace SAEA.FTP.Core
{
    /// <summary>
    /// 服务端配置与账号管理，配置文件位于 Configs/FTPServer.Config
    /// </summary>
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

        /// <summary>
        /// 读取服务端配置，文件不存在时返回默认配置
        /// </summary>
        /// <returns>服务端配置，不会为 null</returns>
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

        /// <summary>
        /// 修改服务端监听端口与缓冲区大小（仅改内存，需再调用 <see cref="Save"/> 落盘）
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="bufferSize">读写缓冲区大小，单位字节</param>
        public static void Set(ushort port, int bufferSize = 10240)
        {
            _serverConfig.Port = port;
            _serverConfig.BufferSize = 10240;
        }

        /// <summary>
        /// 把当前内存中的服务端配置写入配置文件
        /// </summary>
        public static void Save()
        {
            _configHelper.Write(_serverConfig);
        }

        /// <summary>
        /// 新增或替换账号，替换时会重建用户对象并清空其会话状态
        /// </summary>
        /// <param name="userName">账号名称</param>
        /// <param name="password">账号密码</param>
        /// <param name="root">该账号可访问的根目录，为空时使用程序目录</param>
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

        /// <summary>
        /// 按名称取账号，并补齐其数据缓冲实例
        /// </summary>
        /// <param name="userName">账号名称</param>
        /// <returns>账号对象；账号不存在时返回 null</returns>
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

        /// <summary>
        /// 校验账号密码并标记登录状态；anonymous 账号不校验密码
        /// </summary>
        /// <param name="userName">账号名称</param>
        /// <param name="password">账号密码</param>
        /// <returns>登录成功返回 true，账号不存在或密码错误返回 false</returns>
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

        /// <summary>
        /// 删除指定账号
        /// </summary>
        /// <param name="userName">账号名称</param>
        public static void DelUser(string userName)
        {
            if (_serverConfig != null && _serverConfig.Users != null)
            {
                _serverConfig.Users.TryRemove(userName, out FTPUser user);
            }
        }

        #region user binding

        /// <summary>
        /// 把连接标识与账号名称绑定，重复绑定会覆盖旧值
        /// </summary>
        /// <param name="id">连接唯一标识</param>
        /// <param name="userName">绑定的账号名称</param>
        public static void UserBinding(string id, string userName)
        {
            _userBinds.AddOrUpdate(id, userName, (k, v) => userName);
        }

        /// <summary>
        /// 按连接标识取绑定的账号名称
        /// </summary>
        /// <param name="id">连接唯一标识</param>
        /// <returns>绑定的账号名称；未绑定时返回空字符串</returns>
        public static string GetUserBind(string id)
        {
            if (_userBinds.TryGetValue(id, out string userName))
            {
                return userName;
            }
            return string.Empty;
        }

        /// <summary>
        /// 解除连接标识与账号名称的绑定
        /// </summary>
        /// <param name="id">连接唯一标识</param>
        public static void RemoveUserBind(string id)
        {
            _userBinds.TryRemove(id, out string userName);
        }

        #endregion
    }
}
