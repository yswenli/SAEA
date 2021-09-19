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
    /// FTPCommand,
    /// http://mina.apache.org/ftpserver-project/ftpserver_commands.html
    /// </summary>
    public enum FTPCommand
    {
        /// <summary>
        /// 用户名
        /// </summary>
        USER = 1,
        /// <summary>
        /// 密码
        /// </summary>
        PASS = 2,
        /// <summary>
        /// 系统
        /// </summary>
        SYST = 3,
        /// <summary>
        /// 心跳
        /// </summary>
        NOOP = 4,
        /// <summary>
        /// 创建数据连接
        /// </summary>
        PASV = 5,
        /// <summary>
        /// 创建客户端连接
        /// </summary>
        PORT = 51,
        /// <summary>
        /// 目录列表
        /// </summary>
        LIST = 6,
        /// <summary>
        /// 目录列表
        /// </summary>
        MLSD = 7,
        /// <summary>
        /// 目录列表
        /// </summary>
        NLST = 8,
        /// <summary>
        /// 当前工作目录
        /// </summary>
        CWD = 9,
        /// <summary>
        /// 父目录
        /// </summary>
        CDUP = 10,
        /// <summary>
        /// 指定目录为当前工作目录
        /// </summary>
        PWD = 11,
        /// <summary>
        /// 创建目录
        /// </summary>
        MKD = 12,
        /// <summary>
        /// 删除目录
        /// </summary>
        RMD = 13,
        /// <summary>
        /// 重命名
        /// </summary>
        RNFR = 14,
        /// <summary>
        /// 重命名 后接命令
        /// </summary>
        RNTO = 15,
        /// <summary>
        /// 删除文件
        /// </summary>
        DELE = 16,
        /// <summary>
        /// 上传
        /// </summary>
        STOR = 17,
        /// <summary>
        /// 下载
        /// </summary>
        RETR = 18,
        /// <summary>
        /// 获取文件大小
        /// </summary>
        SIZE = 20,
        /// <summary>
        /// 选项
        /// </summary>
        OPTS = 21,
        /// <summary>
        /// 字符串
        /// </summary>
        FEAT = 22,
        /// <summary>
        /// 类型
        /// </summary>
        TYPE = 23,
        /// <summary>
        /// 退出命令
        /// </summary>
        QUIT = 99
    }
}
