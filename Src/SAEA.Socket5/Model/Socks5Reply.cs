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
*文件名： Socks5Reply
*版本号： v26.11.5.1
*唯一标识：d8c1761c-0bc4-41df-876e-9452e32fc692
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/9/20 02:53:58
*描述：解析后的 SOCKS5 响应
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
 * 文件名：Socks5Reply.cs
 * 版本号：v26.9.20.1
 * 描述：解析后的 SOCKS5 响应
 */

namespace SAEA.Socket5.Model
{
    /// <summary>
    /// 解析后的 SOCKS5 响应（服务端 → 客户端）
    /// </summary>
    public class Socks5Reply
    {
        /// <summary>
        /// 响应码
        /// </summary>
        public Socks5ReplyCode ReplyCode { get; set; }

        /// <summary>
        /// 绑定地址类型
        /// </summary>
        public Socks5AddressType AddressType { get; set; }

        /// <summary>
        /// 绑定主机（IP 字符串或域名）
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 绑定端口
        /// </summary>
        public int Port { get; set; }
    }
}
