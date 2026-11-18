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
*文件名： RouteTokenApiController
*版本号： net10.0
*唯一标识：c591fd1c-a858-49aa-a435-7772cacb9b13
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 10:46:20
*描述：用户原始写法 api/v1/[controller]/[action]/{id} 的令牌用例控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 10:46:20
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证多段静态前缀与两个令牌混排、异步方法名的 Async 后缀剥离
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 用户原始写法：多段静态前缀后接 [controller]、[action] 与路径参数，
    /// 绝对路径为 /api/v1/routetokenapi/get/{id}。
    /// </summary>
    public class RouteTokenApiController : Controller
    {
        /// <summary>
        /// 异步 action 的令牌入口，[action] 应剥掉 Async 后缀解析为 get。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [HttpGet("api/v1/[controller]/[action]/{id}")]
        public async Task<string> GetAsync(string id)
        {
            await Task.Yield();

            return "token-api:" + id;
        }
    }
}