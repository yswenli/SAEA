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
*文件名： FTPClientConfigManager
*版本号： v26.4.23.1
*唯一标识：d76f765d-3c51-4216-87be-c651b6c0e8db
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 17:53:24
*描述：FTPClientConfigManager接口
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

namespace SAEA.FTP.Core
{
    /// <summary>
    /// 客户端配置读写管理，配置文件位于 Configs/FTPClient.config
    /// </summary>
    public static class FTPClientConfigManager
    {
        static ClientConfig _config;

        static ConfigHelper<ClientConfig> _configHelper;


        static FTPClientConfigManager()
        {
            _configHelper = new ConfigHelper<ClientConfig>("Configs", "FTPClient.config");
        }


        /// <summary>
        /// 读取客户端配置，文件不存在时返回默认配置
        /// </summary>
        /// <returns>客户端配置，不会为 null</returns>
        public static ClientConfig Get()
        {
            var cc = _configHelper.Read();

            if (cc == null)
            {
                cc = new ClientConfig();
            }
            _config = cc;
            return cc;
        }

        /// <summary>
        /// 使用指定服务器地址与凭据更新当前配置并写入配置文件，须先调用 <see cref="Get"/> 载入配置
        /// </summary>
        /// <param name="ip">服务器 IP 地址</param>
        /// <param name="port">服务器控制端口</param>
        /// <param name="userName">登录用户名</param>
        /// <param name="password">登录密码</param>
        public static void Set(string ip, int port, string userName, string password)
        {
            _config.IP = ip;
            _config.Port = port;
            _config.UserName = userName;
            _config.Password = password;
            _configHelper.Write(_config);
        }
    }
}
