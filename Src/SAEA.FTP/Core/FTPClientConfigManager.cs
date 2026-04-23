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
*修改时间：2019/11/11 17:53:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：FTPClientConfigManager接口
*
*****************************************************************************/
using SAEA.Common;

namespace SAEA.FTP.Core
{
    public static class FTPClientConfigManager
    {
        static ClientConfig _config;

        static ConfigHelper<ClientConfig> _configHelper;


        static FTPClientConfigManager()
        {
            _configHelper = new ConfigHelper<ClientConfig>("Configs", "FTPClient.config");
        }


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
