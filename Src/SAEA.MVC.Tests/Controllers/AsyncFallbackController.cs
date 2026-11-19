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
*文件名： AsyncFallbackController
*版本号： net10.0
*唯一标识：02890b06-5087-49a4-9d5f-f990476412cb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 17:55:17
*描述：两段式路由 Async 后缀回退用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 17:55:17
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供只声明 Async 版本、同步与异步同名共存、以及只走属性路由模板的三个入口
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 两段式路由 Async 后缀回退用例的控制器：普通 Controller，不继承 WSController。
    /// </summary>
    public class AsyncFallbackController : Controller
    {
        /// <summary>
        /// 只声明 Async 后缀版本的入口，两段式地址应能省略 Async。
        /// </summary>
        /// <returns>固定文本</returns>
        public async Task<string> PlainAsync()
        {
            await Task.Yield();

            return "fallback-plain";
        }

        /// <summary>
        /// 同步版本，与 BothAsync 同名不同后缀共存，精确名应优先命中。
        /// </summary>
        /// <returns>固定文本</returns>
        public string Both()
        {
            return "fallback-both-sync";
        }

        /// <summary>
        /// 异步版本，仅在请求地址带 Async 后缀时命中。
        /// </summary>
        /// <returns>固定文本</returns>
        public async Task<string> BothAsync()
        {
            await Task.Yield();

            return "fallback-both-async";
        }

        /// <summary>
        /// 只声明属性路由模板的入口，用于确认属性路由按模板匹配而不是按方法名。
        /// </summary>
        /// <returns>固定文本</returns>
        [Route("/asyncfallback/attr-only")]
        public async Task<string> AttrOnlyAsync()
        {
            await Task.Yield();

            return "fallback-attr";
        }
    }
}