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
*文件名： EventStreamProbeController
*版本号： net10.0
*唯一标识：4dac783d-5202-4372-aee9-c72e7df2548e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 16:54:00
*描述：事件流用例的控制器，只暴露一个 GET 入口供 HEAD 语义验证
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供可被 HEAD 命中的事件流入口
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 事件流用例控制器：只声明 GET，HEAD 走 GET 兜底并抑制正文。
    /// </summary>
    public class EventStreamProbeController : Controller
    {
        /// <summary>
        /// 返回事件流结果，构造时即写出报头并启动心跳。
        /// </summary>
        /// <returns>事件流结果</returns>
        [HttpGetAttribute("events/probe")]
        public ActionResult Probe()
        {
            return GetEventStream();
        }
    }
}