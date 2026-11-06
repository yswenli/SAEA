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
*文件名： ClientConfig
*版本号： v26.4.23.1
*唯一标识：a3879fc5-1174-4937-9ba3-e96065d5a4bc
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：ClientConfig接口
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

namespace SAEA.FTP
{
    /// <summary>
    /// FTP 客户端配置：服务器地址、登录凭据、收发缓冲区与各类超时
    /// </summary>
    public class ClientConfig
    {
        /// <summary>
        /// 服务器 IP 地址
        /// </summary>
        public string IP
        {
            get; set;
        }

        /// <summary>
        /// 服务器控制端口
        /// </summary>
        public int Port
        {
            get; set;
        }

        /// <summary>
        /// 登录用户名
        /// </summary>
        public string UserName
        {
            get; set;
        }

        /// <summary>
        /// 登录密码
        /// </summary>
        public string Password
        {
            get; set;
        }

        /// <summary>
        /// 收发缓冲区大小，单位字节，默认 64KB
        /// </summary>
        public int BufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// 建立控制连接超时
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 控制命令响应超时
        /// </summary>
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 数据连接读写超时
        /// </summary>
        public TimeSpan DataTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 控制命令与目录列表编码，默认 UTF-8
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }
}
