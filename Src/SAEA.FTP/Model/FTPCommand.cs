/****************************************************************************
*项目名称：SAEA.FTP.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Model
*类 名 称：FTPCommand
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/7 17:48:57
*描述：
*=====================================================================
*修改时间：2019/11/7 17:48:57
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Model
{
    /// <summary>
    /// FTPCommand
    /// </summary>
    public enum FTPCommand
    {
        USER = 1,
        PASS = 2,
        SYST = 3,
        NOOP = 4,
        PASV = 5,
        List = 6,
        MLSD = 7,
        NLST = 8,
        CWD = 9,
        CDUP = 10,
        PWD = 11,
        MKD = 12,
        RMD = 13,
        RNFR=14,
        RNTO=15,




        REST = 14,
        QUIT = 99
    }
}
