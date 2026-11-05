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
*修改时间：2019/09/27 17:50:27
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ServerConfig接口
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

    public class ServerConfig
    {
        public string IP { get; set; } = "127.0.0.1";

        public ushort Port
        {
            get; set;
        } = 21;

        public int BufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// 被动模式端口范围；都为 0 时由系统分配
        /// </summary>
        public ushort PassivePortMin { get; set; } = 0;

        public ushort PassivePortMax { get; set; } = 0;

        /// <summary>
        /// 同一账号允许的并发会话数；0 表示不限制
        /// </summary>
        public int MaxSessionsPerUser { get; set; } = 0;

        /// <summary>
        /// 等待客户端建立数据连接的超时
        /// </summary>
        public TimeSpan DataTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public FTPOverwritePolicy OverwritePolicy { get; set; } = FTPOverwritePolicy.Overwrite;

        public ConcurrentDictionary<string, FTPUser> Users { get; set; } = new ConcurrentDictionary<string, FTPUser>();
    }
}
