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
*文件名： RouteTokenCaseController
*版本号： net10.0
*唯一标识：9fc9c8fe-5c63-4cba-ad91-4f96b707ea6f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 10:45:00
*描述：令牌大小写混写用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 10:45:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证 [Controller] 与 [ACTION] 与全小写令牌等价
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 令牌大小写混写：[Controller] / [ACTION] 与全小写写法等价，
    /// 绝对路径为 /tokencase/routetokencase/echo/{id}。
    /// </summary>
    [Route("tokencase/[Controller]")]
    public class RouteTokenCaseController : Controller
    {
        /// <summary>
        /// 大小写混写的令牌入口。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [HttpGetAttribute("[ACTION]/{id}")]
        public string EchoAsync(string id)
        {
            return "tokencase:" + id;
        }
    }
}