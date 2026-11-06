/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Socket5.Model
*文件名： Socks5Request
*版本号： v26.11.5.1
*唯一标识：14238bba-7642-4ce2-953a-e09f7fe3ac31
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/9/20 02:53:58
*描述：解析后的 SOCKS5 请求
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Model
 * 文件名：Socks5Request.cs
 * 版本号：v26.9.20.1
 * 描述：解析后的 SOCKS5 请求
 */

namespace SAEA.Socket5.Model
{
    /// <summary>
    /// 解析后的 SOCKS5 请求（客户端 → 服务端）
    /// </summary>
    public class Socks5Request
    {
        /// <summary>
        /// 命令
        /// </summary>
        public Socks5Command Command { get; set; }

        /// <summary>
        /// 地址类型
        /// </summary>
        public Socks5AddressType AddressType { get; set; }

        /// <summary>
        /// 目标主机（IPv4/IPv6 为 IP 字符串，Domain 为域名）
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 目标端口
        /// </summary>
        public int Port { get; set; }
    }
}
