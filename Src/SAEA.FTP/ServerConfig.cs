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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP
{
    public class ServerConfig
    {
        public string IP
        {
            get; set;
        }

        public int Port
        {
            get; set;
        }

        public int DataPort
        {
            get; set;
        }
    }
}
