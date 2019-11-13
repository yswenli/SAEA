/****************************************************************************
*项目名称：SAEA.FTP.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Model
*类 名 称：FTPUser
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 13:43:51
*描述：
*=====================================================================
*修改时间：2019/11/11 13:43:51
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using Newtonsoft.Json;
using SAEA.FTP.Core;

namespace SAEA.FTP.Model
{
    public class FTPUser
    {
        public string UserName { get; set; }

        public string Password { get; set; }

        public int DataPort { get; set; } = 22;

        public string Root { get; set; } = "/";

        [JsonIgnore]
        public FTPDataManager FTPDataManager { get; set; }

        [JsonIgnore]
        public bool IsLogin { get; set; } = false;

        [JsonIgnore]
        public string CurrentFtpPath { get; set; } = "/";

        public FTPUser()
        {

        }

        public FTPUser(string userName, string password, int dataPort, string root)
        {
            this.UserName = userName;
            this.Password = password;
            this.DataPort = dataPort;
            this.Root = root;
            this.FTPDataManager = new FTPDataManager();
        }
    }
}
