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
*文件名： RestVerbController
*版本号： net10.0
*唯一标识：407213c8-3a68-4e37-a9f5-11bfb71650b0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 16:02:33
*描述：RESTful 动词用例的控制器，覆盖 PUT/DELETE/PATCH/HEAD/OPTIONS 与两段式、属性路由两条路径
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 [Route]-only 与显式 [HttpHead]/[HttpOptions] 同路径入口，以及 GET 与显式 OPTIONS 同路径入口，供冲突判定与 Allow 用例使用
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 动词用例控制器：两段式入口不带模板，属性路由入口带模板，两者覆盖同一批新动词。
    /// </summary>
    public class RestVerbController : Controller
    {
        /// <summary>
        /// 两段式只声明 PUT 的入口。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpPutAttribute]
        public string PutOnly()
        {
            return "rest-put-only";
        }

        /// <summary>
        /// 两段式只声明 DELETE 的入口。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpDeleteAttribute]
        public string DeleteOnly()
        {
            return "rest-delete-only";
        }

        /// <summary>
        /// 两段式只声明 PATCH 的入口。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpPatchAttribute]
        public string PatchOnly()
        {
            return "rest-patch-only";
        }

        /// <summary>
        /// 两段式不声明任何动词的入口，所有请求方法都应放行。
        /// </summary>
        /// <returns>固定文本</returns>
        public string AnyVerb()
        {
            return "rest-any-verb";
        }

        /// <summary>
        /// 属性路由只声明 PUT 的入口。
        /// </summary>
        /// <param name="id">路径参数</param>
        /// <returns>拼接后的固定文本</returns>
        [HttpPutAttribute("rest/put/{id}")]
        public string PutAttr(string id)
        {
            return "rest-attr-put-" + id;
        }

        /// <summary>
        /// 属性路由只声明 DELETE 的入口。
        /// </summary>
        /// <param name="id">路径参数</param>
        /// <returns>拼接后的固定文本</returns>
        [HttpDeleteAttribute("rest/delete/{id}")]
        public string DeleteAttr(string id)
        {
            return "rest-attr-delete-" + id;
        }

        /// <summary>
        /// 属性路由只声明 PATCH 的入口。
        /// </summary>
        /// <param name="id">路径参数</param>
        /// <returns>拼接后的固定文本</returns>
        [HttpPatchAttribute("rest/patch/{id}")]
        public string PatchAttr(string id)
        {
            return "rest-attr-patch-" + id;
        }

        /// <summary>
        /// 只声明 GET 的属性路由入口，HEAD 应复用它的执行结果但不发正文。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute("rest/head-get")]
        public string HeadGet()
        {
            return "rest-head-get-body";
        }

        /// <summary>
        /// 与显式 HEAD 声明同路径的 GET 入口，HEAD 请求不应命中它。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute("rest/head-explicit")]
        public string HeadGetAlias()
        {
            return "rest-head-get-alias";
        }

        /// <summary>
        /// 同路径显式声明 HEAD 的入口，必须优先于 GET 别名。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpHeadAttribute("rest/head-explicit")]
        public string HeadExplicit()
        {
            return "rest-head-explicit";
        }

        /// <summary>
        /// 只声明 HEAD 的属性路由入口，Allow 中不应出现 GET。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpHeadAttribute("rest/head-only")]
        public string HeadOnly()
        {
            return "rest-head-only";
        }

        /// <summary>
        /// 显式声明 OPTIONS 的属性路由入口，OPTIONS 请求应执行它。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpOptionsAttribute("rest/options-explicit")]
        public string OptionsExplicit()
        {
            return "rest-options-explicit";
        }

        /// <summary>
        /// 只声明 GET 的属性路由入口，OPTIONS 应自动应答 204 而不是执行它。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute("rest/options-auto")]
        public string OptionsAuto()
        {
            return "rest-options-auto";
        }

        /// <summary>
        /// 同时声明 GET 与 POST 的属性路由入口，用于验证 405 的 Allow 顺序。
        /// 同路径只能有一个带模板的特性，另一个动词不带模板。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute("rest/both")]
        [HttpPostAttribute]
        public string BothVerbs()
        {
            return "rest-both";
        }

        /// <summary>
        /// 只声明 OPTIONS 的属性路由入口，其它动词应得到 405 且 Allow 只有 OPTIONS。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpOptionsAttribute("rest/options-only")]
        public string OptionsOnly()
        {
            return "rest-options-only";
        }

        /// <summary>
        /// 只声明模板、不带任何谓词特性的属性路由入口，所有非 OPTIONS 方法都应放行。
        /// </summary>
        /// <returns>固定文本</returns>
        [RouteAttribute("rest/route-only")]
        public string RouteOnly()
        {
            return "rest-route-only";
        }

        /// <summary>
        /// 与显式 HEAD 声明同路径的不限谓词入口，两者不应被判为冲突。
        /// </summary>
        /// <returns>固定文本</returns>
        [RouteAttribute("rest/route-plus-head")]
        public string RouteOnlyWithHead()
        {
            return "rest-route-plus-head";
        }

        /// <summary>
        /// 同路径显式声明 HEAD 的入口，必须优先于不限谓词的 GET 兜底。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpHeadAttribute("rest/route-plus-head")]
        public string ExplicitHeadOnRouteOnly()
        {
            return "rest-route-plus-head-explicit";
        }

        /// <summary>
        /// 与显式 OPTIONS 声明同路径的不限谓词入口，两者不应被判为冲突。
        /// </summary>
        /// <returns>固定文本</returns>
        [RouteAttribute("rest/route-plus-options")]
        public string RouteOnlyWithOptions()
        {
            return "rest-route-plus-options";
        }

        /// <summary>
        /// 同路径显式声明 OPTIONS 的入口，OPTIONS 请求应执行它而不是自动应答。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpOptionsAttribute("rest/route-plus-options")]
        public string ExplicitOptionsOnRouteOnly()
        {
            return "rest-route-plus-options-explicit";
        }

        /// <summary>
        /// 与显式 OPTIONS 声明同路径的 GET 入口，用于验证 405 的 Allow 会追加 OPTIONS。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute("rest/get-plus-options")]
        public string GetWithExplicitOptions()
        {
            return "rest-get-plus-options";
        }

        /// <summary>
        /// 同路径显式声明 OPTIONS 的入口，与 GET 入口共存且不冲突。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpOptionsAttribute("rest/get-plus-options")]
        public string ExplicitOptionsOnGet()
        {
            return "rest-get-plus-options-explicit";
        }
    }
}