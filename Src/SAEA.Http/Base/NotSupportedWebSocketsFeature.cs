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
 *命名空间：SAEA.Http.Base
 *文件名： NotSupportedWebSocketsFeature
 *描述：默认的不支持升级的 WebSocket 能力实现
 *
 *****************************************************************************/
using SAEA.Http.Model;

using System;
using System.Threading.Tasks;

namespace SAEA.Http.Base
{
    /// <summary>
    /// 默认的 WebSocket 特性实现：不支持升级，只提供只读的请求判断。
    /// 需要 WebSocket 能力的上下文（如 SAEA.MVC）请覆盖 HttpContextBase.WebSockets。
    /// </summary>
    internal class NotSupportedWebSocketsFeature : IWebSocketsFeature
    {
        readonly IHttpContext _context;

        public NotSupportedWebSocketsFeature(IHttpContext context)
        {
            _context = context;
        }

        public bool IsWebSocketRequest
        {
            get
            {
                var headers = _context?.Request?.Headers;

                if (headers == null) return false;

                if (!headers.TryGetValue("Upgrade", out var upgrade) || string.IsNullOrEmpty(upgrade))
                    return false;

                if (upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (!headers.TryGetValue("Connection", out var connection) || string.IsNullOrEmpty(connection))
                    return false;

                if (connection.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (!headers.TryGetValue("Sec-WebSocket-Key", out var key) || string.IsNullOrWhiteSpace(key))
                    return false;

                return true;
            }
        }

        public IWebSocketConnection Current => null;

        public Task<IWebSocketConnection> AcceptWebSocketAsync(string subProtocol = null)
        {
            throw new NotSupportedException("当前 HttpContext 不支持 WebSocket。请使用 SAEA.MVC 的 WSController。");
        }
    }
}