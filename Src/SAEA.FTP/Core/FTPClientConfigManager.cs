/****************************************************************************
*项目名称：SAEA.FTP.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Core
*类 名 称：FTPClientConfigManager
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 17:48:55
*描述：
*=====================================================================
*修改时间：2019/11/11 17:48:55
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
