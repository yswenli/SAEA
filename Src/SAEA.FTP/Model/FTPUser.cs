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
*修改时间：2026/11/11 11:45:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除指向已废弃数据连接管理器的 FTPDataSocketManager 属性
*
*****************************************************************************/
using SAEA.Common.Newtonsoft.Json;
using SAEA.FTP.Core;

namespace SAEA.FTP.Model
{
    /// <summary>
    /// 服务端账号信息，包含根目录、登录状态与数据缓冲
    /// </summary>
    public class FTPUser
    {
        /// <summary>
        /// 账号名称
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 账号密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 数据连接端口，默认 22
        /// </summary>
        public int DataPort { get; set; } = 22;

        /// <summary>
        /// 账号可访问的根目录，默认 C:\
        /// </summary>
        public string Root { get; set; } = "C:\\";

        /// <summary>
        /// 该账号的数据接收缓冲，不参与配置序列化
        /// </summary>
        [JsonIgnore]
        public FTPDataManager FTPDataManager { get; set; }

        /// <summary>
        /// 是否已通过登录校验，不参与配置序列化
        /// </summary>
        [JsonIgnore]
        public bool IsLogin { get; set; } = false;

        /// <summary>
        /// 当前 FTP 虚拟路径，默认根目录 /
        /// </summary>
        [JsonIgnore]
        public string CurrentFtpPath { get; set; } = "/";

        /// <summary>
        /// 当前映射的本地文件系统路径，默认为 C:\
        /// </summary>
        [JsonIgnore]
        public string CurrentPath { get; set; } = "C:\\";

        /// <summary>
        /// 创建空账号，属性取默认值，供配置反序列化使用
        /// </summary>
        public FTPUser()
        {

        }

        /// <summary>
        /// 以指定凭据与根目录创建账号，并初始化其数据缓冲
        /// </summary>
        /// <param name="userName">账号名称</param>
        /// <param name="password">账号密码</param>
        /// <param name="root">账号可访问的根目录</param>
        public FTPUser(string userName, string password, string root)
        {
            this.UserName = userName;
            this.Password = password;
            this.Root = root;
            this.FTPDataManager = new FTPDataManager();
        }
    }
}
