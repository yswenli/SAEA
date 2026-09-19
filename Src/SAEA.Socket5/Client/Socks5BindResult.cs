/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Client
 * 文件名：Socks5BindResult.cs
 * 版本号：v26.9.20.1
 * 描述：BIND 命令的返回结果
 */

using System.IO;

using SAEA.Socket5.Model;

namespace SAEA.Socket5.Client
{
    /// <summary>
    /// BIND 命令的返回结果
    /// </summary>
    public class Socks5BindResult
    {
        /// <summary>
        /// 控制流（BIND 完成后即为与目标对端的隧道）
        /// </summary>
        public Stream ControlStream { get; set; }

        /// <summary>
        /// 服务端第一阶段响应（包含监听地址/端口）
        /// </summary>
        public Socks5Reply FirstReply { get; set; }

        /// <summary>
        /// 等待服务端第二阶段响应（远端连入后）
        /// </summary>
        public System.Func<Socks5Reply> WaitForBindComplete { get; set; }
    }
}
