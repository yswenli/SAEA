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
*命名空间：SAEA.Http.Model
*文件名： IWebSocketsFeature
*版本号： v26.11.5.1
*唯一标识：891fea02-8793-4f1b-ace9-ef024bace944
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:56:30
*描述：当前 Http 上下文的 WebSocket 能力入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 当前 Http 上下文的 WebSocket 能力入口（对应 ASP.NET Core 的 HttpContext.WebSockets）
    /// </summary>
    public interface IWebSocketsFeature
    {
        /// <summary>
        /// 当前请求是否为 WebSocket 升级请求（只读请求头，不产生副作用）
        /// </summary>
        bool IsWebSocketRequest { get; }

        /// <summary>
        /// 当前上下文已经接受的连接；未接受前为 null
        /// </summary>
        IWebSocketConnection Current { get; }

        /// <summary>
        /// 接受 WebSocket 连接：完成握手并写 101，之后连接交给调用方使用
        /// </summary>
        /// <param name="subProtocol">需要回显的子协议；为 null 时不回显</param>
        /// <returns>已建立的连接</returns>
        Task<IWebSocketConnection> AcceptWebSocketAsync(string subProtocol = null);
    }
}