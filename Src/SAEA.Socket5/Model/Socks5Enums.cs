/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Model
 * 文件名：Socks5Enums.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 协议相关枚举（RFC1928 / RFC1929）
 */

namespace SAEA.Socket5.Model
{
    /// <summary>
    /// SOCKS 版本
    /// </summary>
    public enum Socks5Version : byte
    {
        /// <summary>
        /// SOCKS5
        /// </summary>
        V5 = 0x05
    }

    /// <summary>
    /// 认证方式（RFC1928）
    /// </summary>
    public enum Socks5AuthMethod : byte
    {
        /// <summary>
        /// 无认证
        /// </summary>
        NoAuth = 0x00,

        /// <summary>
        /// 用户名/密码（RFC1929）
        /// </summary>
        UserPass = 0x02,

        /// <summary>
        /// 无可接受的方法
        /// </summary>
        NoAcceptable = 0xFF
    }

    /// <summary>
    /// 命令（RFC1928）
    /// </summary>
    public enum Socks5Command : byte
    {
        /// <summary>
        /// 建立到目标主机的连接
        /// </summary>
        Connect = 0x01,

        /// <summary>
        /// 绑定，被动监听并等待远端连入
        /// </summary>
        Bind = 0x02,

        /// <summary>
        /// UDP 关联
        /// </summary>
        UdpAssociate = 0x03
    }

    /// <summary>
    /// 地址类型（RFC1928）
    /// </summary>
    public enum Socks5AddressType : byte
    {
        /// <summary>
        /// IPv4
        /// </summary>
        IPv4 = 0x01,

        /// <summary>
        /// 域名
        /// </summary>
        Domain = 0x03,

        /// <summary>
        /// IPv6
        /// </summary>
        IPv6 = 0x04
    }

    /// <summary>
    /// 响应码 REP（RFC1928）
    /// </summary>
    public enum Socks5ReplyCode : byte
    {
        /// <summary>
        /// 成功
        /// </summary>
        Succeeded = 0x00,

        /// <summary>
        /// 常规失败
        /// </summary>
        GeneralFailure = 0x01,

        /// <summary>
        /// 规则不允许连接
        /// </summary>
        ConnectionNotAllowed = 0x02,

        /// <summary>
        /// 网络不可达
        /// </summary>
        NetworkUnreachable = 0x03,

        /// <summary>
        /// 主机不可达
        /// </summary>
        HostUnreachable = 0x04,

        /// <summary>
        /// 连接被拒绝
        /// </summary>
        ConnectionRefused = 0x05,

        /// <summary>
        /// TTL 超时
        /// </summary>
        TtlExpired = 0x06,

        /// <summary>
        /// 命令不支持
        /// </summary>
        CommandNotSupported = 0x07,

        /// <summary>
        /// 地址类型不支持
        /// </summary>
        AddressTypeNotSupported = 0x08
    }

    /// <summary>
    /// 用户名/密码认证状态（RFC1929）
    /// </summary>
    public enum Socks5AuthStatus : byte
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success = 0x00,

        /// <summary>
        /// 失败
        /// </summary>
        Failure = 0x01
    }
}
