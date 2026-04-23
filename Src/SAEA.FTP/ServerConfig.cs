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
*命名空间：SAEA.FTP
*文件名： ServerConfig
*版本号： v26.4.23.1
*唯一标识：c36178ff-8080-4785-b5fe-a8824ec20ef1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/09/27 17:50:27
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
