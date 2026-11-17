/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Controllers
*文件名： RouteWsController
*版本号： net10.0
*唯一标识：f798d3dd-fcc3-4d50-a3f4-6f068cf215f6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 18:20:00
*描述：属性路由声明的 WebSocket 入口控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证 WS 入口用 [Route] 声明后可升级并回显
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// WebSocket 入口控制器：握手地址由 [Route] 声明，收到文本后原样回写。
    /// </summary>
    public class RouteWsController : WSController
    {
        /// <summary>
        /// 用 [Route] 声明的握手入口。
        /// </summary>
        /// <returns>握手完成的任务</returns>
        [WebSocket]
        [Route("routedemo/ws")]
        public override Task OpenAsync()
        {
            return base.OpenAsync();
        }

        /// <summary>
        /// 收到一条完整消息后把文本原样回写给对端。
        /// </summary>
        /// <param name="message">收到的消息</param>
        /// <returns>发送完成的任务</returns>
        public override Task MessageAsync(WSMessage message)
        {
            if (message == null || !message.IsText)
                return Task.CompletedTask;

            return WebSockets.Current.SendAsync(message.Text);
        }
    }
}