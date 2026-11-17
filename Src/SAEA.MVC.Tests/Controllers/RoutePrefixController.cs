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
*文件名： RoutePrefixController
*版本号： net10.0
*唯一标识：9cf4a650-4575-474f-bc74-f13a7666dfff
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 18:20:00
*描述：类级与方法级路由模板拼接用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证类级模板与方法级模板按 / 拼接
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 类级 [Route] 前缀控制器：方法级模板拼接在类级模板之后。
    /// </summary>
    [Route("routedemo/prefix")]
    public class RoutePrefixController : Controller
    {
        /// <summary>
        /// 方法级模板入口，绝对路径为 /routedemo/prefix/child。
        /// </summary>
        /// <returns>固定文本</returns>
        [Route("child")]
        public string Child()
        {
            return "routedemo-prefix-child-ok";
        }
    }
}