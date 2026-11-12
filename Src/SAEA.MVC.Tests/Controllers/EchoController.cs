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
*文件名： EchoController
*版本号： net10.0
*唯一标识：51b03b55-0b8e-4976-895e-3ddf56677287
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：WebSocket 回显控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:05:27
*修改人： yswenli
*版本号： net10.0
*描述：新增，为 WS/WSS 升级与回显用例提供入口
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// WebSocket 回显控制器：握手走基类默认实现，收到文本消息后原样回写。
    /// </summary>
    public class EchoController : WSController
    {
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