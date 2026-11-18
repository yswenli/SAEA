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
*文件名： RouteTokenPrefixController
*版本号： net10.0
*唯一标识：a43e08a0-34d8-4067-a796-04e986531aba
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 10:45:00
*描述：类级模板使用 [controller]、方法级模板使用 [action] 的用例控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 10:45:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证类级与方法级模板各自解析自己的令牌
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 类级模板带 [controller]、方法级模板带 [action]：
    /// 绝对路径为 /token/routetokenprefix/echo/{id}。
    /// </summary>
    [Route("token/[controller]")]
    public class RouteTokenPrefixController : Controller
    {
        /// <summary>
        /// 方法级令牌入口。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [HttpGet("[action]/{id}")]
        public string EchoAsync(string id)
        {
            return "token-prefix:" + id;
        }
    }
}