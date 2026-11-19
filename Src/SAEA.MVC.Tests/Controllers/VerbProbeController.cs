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
*文件名： VerbProbeController
*版本号： net10.0
*唯一标识：3f6a1c9e-5d47-4b2a-9c81-0e7d4b95a213
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 21:00:00
*描述：两段式路由动词校验用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 21:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，只提供两段式地址，覆盖只声明 GET、只声明 POST、两者都声明与都不声明四种情况
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 两段式路由动词校验的控制器：每个动作只声明需要验证的 HTTP 动词组合，不带任何路由模板。
    /// </summary>
    public class VerbProbeController : Controller
    {
        /// <summary>
        /// 只声明 GET 的入口。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute]
        public string GetOnly()
        {
            return "verbprobe-get-only";
        }

        /// <summary>
        /// 只声明 POST 的入口。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpPostAttribute]
        public string PostOnly()
        {
            return "verbprobe-post-only";
        }

        /// <summary>
        /// 同时声明 GET 与 POST 的入口，两种动词都应放行。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute]
        [HttpPostAttribute]
        public string Both()
        {
            return "verbprobe-both";
        }

        /// <summary>
        /// 不声明任何动词的入口，所有请求方法都应放行。
        /// </summary>
        /// <returns>固定文本</returns>
        public string Any()
        {
            return "verbprobe-any";
        }
    }
}