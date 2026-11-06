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
*命名空间：SAEA.Socket5.Client
*文件名： Socks5BindResult
*版本号： v26.11.5.1
*唯一标识：55a3e7a1-f154-4dc8-a075-4b8edb851533
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/9/20 02:53:58
*描述：BIND 命令的返回结果
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
