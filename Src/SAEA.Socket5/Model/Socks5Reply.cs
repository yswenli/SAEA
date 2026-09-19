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
