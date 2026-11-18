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
*文件名： RouteTokenClassActionController
*版本号： net10.0
*唯一标识：abcba97d-5ccb-4289-8746-e74a84f35315
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 10:45:00
*描述：类级模板含 [action] 令牌的用例控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 10:45:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证类级模板的 [action] 按每个 action 分别替换
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 类级模板含 [action]：每个 action 各自解析出不同路径，互不冲突。
    /// </summary>
    [Route("tokenv/[action]")]
    public class RouteTokenClassActionController : Controller
    {
        /// <summary>
        /// 类级令牌入口之一，绝对路径为 /tokenv/list。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet]
        public string List()
        {
            return "tokenv-list";
        }

        /// <summary>
        /// 类级令牌入口之二，绝对路径为 /tokenv/detail。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet]
        public string Detail()
        {
            return "tokenv-detail";
        }
    }
}