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
 *命名空间：SAEA.MVC
 *文件名： WSController
 *描述：WebSocket 控制器基类
 *
 *****************************************************************************/
using SAEA.Http.Model;

using System;
using System.Threading.Tasks;

namespace SAEA.MVC
{
    /// <summary>
    /// WebSocket 控制器基类。
    /// 路由与普通控制器一致（GET /{controller}/open），OpenAsync 挂了 [WebSocket] 特性。
    /// </summary>
    public abstract class WSController : Controller
    {
        /// <summary>
        /// 当前上下文的 WebSocket 能力入口（等价于 HttpContext.Current.WebSockets）
        /// </summary>
        protected IWebSocketsFeature WebSockets => HttpContext.Current.WebSockets;

        /// <summary>
        /// 连接入口：默认接受握手并立即返回。
        /// 注意不要在 OpenAsync 里跑长循环，MvcInvoker 会同步等待该 Task。
        /// </summary>
        [WebSocket]
        public virtual Task OpenAsync()
        {
            return WebSockets.AcceptWebSocketAsync();
        }

        /// <summary>
        /// 收到一条完整的 Text/Binary 消息（分片已重组）
        /// </summary>
        /// <param name="message">消息</param>
        public virtual Task MessageAsync(WSMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 连接关闭回调
        /// </summary>
        /// <param name="reason">关闭原因</param>
        public virtual Task CloseAsync(WSCloseReason reason)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 连接内发生异常
        /// </summary>
        /// <param name="exception">异常</param>
        public virtual Task ErrorAsync(Exception exception)
        {
            return Task.CompletedTask;
        }
    }
}