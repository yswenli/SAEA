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
*命名空间：SAEA.FTP.Model
*文件名： FTPUser
*版本号： v26.4.23.1
*唯一标识：5f007a55-577a-4b5b-8341-7ac76b7a263e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 15:49:32
*描述：FTPUser类
*
*=====================================================================
*修改标记
*修改时间：2019/11/11 15:49:32
*修改人： yswenli
*版本号： v26.4.23.1
*描述：FTPUser类
*
*****************************************************************************/
using SAEA.Common.Newtonsoft.Json;
using SAEA.FTP.Core;

namespace SAEA.FTP.Model
{
    public class FTPUser
    {
        public string UserName { get; set; }

        public string Password { get; set; }

        public int DataPort { get; set; } = 22;

        public string Root { get; set; } = "C:\\";

        [JsonIgnore]
        public FTPDataManager FTPDataManager { get; set; }

        [JsonIgnore]
        public bool IsLogin { get; set; } = false;

        [JsonIgnore]
        public string CurrentFtpPath { get; set; } = "/";

        [JsonIgnore]
        public string CurrentPath { get; set; } = "C:\\";

        [JsonIgnore]
        public FTPDataSocketManager FTPDataSocketManager { get; set; }

        public FTPUser()
        {

        }

        public FTPUser(string userName, string password, string root)
        {
            this.UserName = userName;
            this.Password = password;
            this.Root = root;
            this.FTPDataManager = new FTPDataManager();
        }
    }
}
