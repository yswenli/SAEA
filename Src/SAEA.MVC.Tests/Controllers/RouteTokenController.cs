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
*文件名： RouteTokenController
*版本号： net10.0
*唯一标识：25f0021a-470b-4935-8a25-de2e14419cdb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 10:45:00
*描述：方法级模板使用 [controller] 与 [action] 令牌的用例控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 10:45:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证两个令牌在方法级模板中替换为类名与方法名
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 方法级模板同时使用 [controller] 与 [action] 令牌：
    /// 类名 RouteTokenController 解析为 routetoken，方法名 EchoAsync 解析为 echo。
    /// </summary>
    public class RouteTokenController : Controller
    {
        /// <summary>
        /// 令牌入口，绝对路径为 /routetoken/echo/{id}。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [HttpGet("[controller]/[action]/{id}")]
        public string EchoAsync(string id)
        {
            return "token-echo:" + id;
        }
    }
}