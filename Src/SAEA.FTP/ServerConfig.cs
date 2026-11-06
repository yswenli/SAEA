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
*唯一标识：2973f320-9c66-4106-8a4c-d1e098b6301c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：ServerConfig接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.FTP.Model;
using System;
using System.Collections.Concurrent;

namespace SAEA.FTP
{
    /// <summary>
    /// 服务端收到 STOR 且目标已存在时的策略
    /// </summary>
    public enum FTPOverwritePolicy
    {
        /// <summary>
        /// 按 FTP 标准覆盖（默认，兼容第三方客户端）
        /// </summary>
        Overwrite,
        /// <summary>
        /// 拒绝覆盖，回复 550
        /// </summary>
        Reject,
        /// <summary>
        /// 自动改名，并在 226 回复中附加 NEWNAME=
        /// </summary>
        Rename
    }

    /// <summary>
    /// FTP 服务端配置：监听地址、端口、缓冲区、被动端口范围、覆盖策略与账号表
    /// </summary>
    public class ServerConfig
    {
        /// <summary>
        /// 服务端监听地址，默认 127.0.0.1
        /// </summary>
        public string IP { get; set; } = "127.0.0.1";

        /// <summary>
        /// 控制连接监听端口，默认 21
        /// </summary>
        public ushort Port
        {
            get; set;
        } = 21;

        /// <summary>
        /// 数据收发缓冲区大小，单位字节，默认 64KB
        /// </summary>
        public int BufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// 被动模式端口范围；都为 0 时由系统分配
        /// </summary>
        public ushort PassivePortMin { get; set; } = 0;

        /// <summary>
        /// 被动模式端口范围上限；小于下限或为 0 时由系统分配
        /// </summary>
        public ushort PassivePortMax { get; set; } = 0;

        /// <summary>
        /// 同一账号允许的并发会话数；0 表示不限制
        /// </summary>
        public int MaxSessionsPerUser { get; set; } = 0;

        /// <summary>
        /// 等待客户端建立数据连接的超时
        /// </summary>
        public TimeSpan DataTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 收到 STOR 且目标已存在时的处理策略，默认覆盖
        /// </summary>
        public FTPOverwritePolicy OverwritePolicy { get; set; } = FTPOverwritePolicy.Overwrite;

        /// <summary>
        /// 账号表，键为账号名称
        /// </summary>
        public ConcurrentDictionary<string, FTPUser> Users { get; set; } = new ConcurrentDictionary<string, FTPUser>();
    }
}
