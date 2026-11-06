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
*文件名： FTPCommand
*版本号： v26.4.23.1
*唯一标识：60feec90-a73b-4eaf-bcc5-135e541a5efe
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/08 09:18:57
*描述：FTPCommand类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Model
{
    /// <summary>
    /// FTP 控制命令枚举，成员名即发送给服务器的命令字，数值仅用于区分
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
        /// 断点偏移
        /// </summary>
        REST = 24,
        /// <summary>
        /// 文件修改时间
        /// </summary>
        MDTM = 25,
        /// <summary>
        /// 扩展被动模式
        /// </summary>
        EPSV = 26,
        /// <summary>
        /// 扩展主动模式
        /// </summary>
        EPRT = 27,
        /// <summary>
        /// 放弃传输
        /// </summary>
        ABOR = 28,
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
