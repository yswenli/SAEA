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
