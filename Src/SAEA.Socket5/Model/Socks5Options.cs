/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Model
 * 文件名：Socks5Options.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 服务端/客户端配置项
 */

using System;

namespace SAEA.Socket5.Model
{
    /// <summary>
    /// SOCKS5 服务端配置
    /// </summary>
    public class Socks5ServerOptions
    {
        /// <summary>
        /// 监听 IP，空表示监听任意地址
        /// </summary>
        public string IP { get; set; } = string.Empty;

        /// <summary>
        /// 监听端口
        /// </summary>
        public int Port { get; set; } = 1080;

        /// <summary>
        /// 允许的认证方式（至少包含 NoAuth 或 UserPass）
        /// </summary>
        public Socks5AuthMethod[] AllowedMethods { get; set; } = new[] { Socks5AuthMethod.NoAuth };

        /// <summary>
        /// 用户名/密码校验器（当 AllowedMethods 含 UserPass 时生效）。
        /// 默认实例不含任何用户，会拒绝全部凭据；启用 UserPass 时请替换为含用户的校验器或自定义实现。
        /// </summary>
        public ISocks5UserValidator UserValidator { get; set; } = new DefaultUserValidator();

        /// <summary>
        /// 读写缓冲区大小（字节）
        /// </summary>
        public int BufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// 单连接动作超时（毫秒）
        /// </summary>
        public int ActionTimeout { get; set; } = 180 * 1000;
    }

    /// <summary>
    /// SOCKS5 客户端配置
    /// </summary>
    public class Socks5ClientOptions
    {
        /// <summary>
        /// 代理服务器地址
        /// </summary>
        public string ProxyHost { get; set; }

        /// <summary>
        /// 代理服务器端口
        /// </summary>
        public int ProxyPort { get; set; } = 1080;

        /// <summary>
        /// 本地绑定 IP（可选，留空表示系统分配）
        /// </summary>
        public string BindIP { get; set; } = string.Empty;

        /// <summary>
        /// 用户名（可选，留空表示使用无认证）
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 密码（可选）
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 连接/动作超时（毫秒）
        /// </summary>
        public int Timeout { get; set; } = 30 * 1000;
    }
}
