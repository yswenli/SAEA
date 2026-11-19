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
*文件名： InheritedActionBaseProbeController
*版本号： net10.0
*唯一标识：a60a81bc-201d-450b-ab72-1ea442f6bcc2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：基类 action 可见性用例的基类控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供只在基类声明的 Inherited 动作，不带任何动词与路由特性
*
*****************************************************************************/
using SAEA.MVC.Controllers;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 基类 action 可见性用例的基类：只声明一个不带任何动词与路由特性的 action。
    /// </summary>
    public class InheritedActionBaseProbeController : Controller
    {
        /// <summary>
        /// 只在基类声明的入口。
        /// </summary>
        /// <returns>固定文本</returns>
        public string Inherited()
        {
            return "inherited-base";
        }
    }
}
