/****************************************************************************
*项目名称：SAEA.FTP
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP
*类 名 称：ServerConfig
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/9/27 15:18:29
*描述：
*=====================================================================
*修改时间：2019/9/27 15:18:29
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.FTP.Model;
using System.Collections.Concurrent;

namespace SAEA.FTP
{
    public class ServerConfig
    {
        public string IP { get; set; } = "127.0.0.1";

        public ushort Port
        {
            get; set;
        } = 21;

        public int BufferSize { get; set; } = 10240;

        public ConcurrentDictionary<string, FTPUser> Users { get; set; } = new ConcurrentDictionary<string, FTPUser>();
    }
}
