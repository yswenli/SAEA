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
*文件名： RouteDemoController
*版本号： net10.0
*唯一标识：8a34f113-ce28-4698-973f-197af7e8299d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 18:20:00
*描述：属性路由端到端用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供静态路径、路径参数、动词限定、单段路由、编码段与输出缓存入口
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 属性路由的 HTTP 动作集合：静态多段路径、路径参数、动词限定、单段路径、
    /// 大小写与编码段、以及带输出缓存的路径参数入口。
    /// </summary>
    public class RouteDemoController : Controller
    {
        /// <summary>
        /// 多段静态路径入口。
        /// </summary>
        /// <returns>固定文本</returns>
        [Route("routedemo/api/static")]
        public string StaticPath()
        {
            return "routedemo-static-ok";
        }

        /// <summary>
        /// 单段路径参数入口，回显绑定到的 id。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [HttpGet("routedemo/echo/{id}")]
        public string Echo(string id)
        {
            return "routedemo-echo:" + id;
        }

        /// <summary>
        /// 与 VerbsPost 共用同一路径的 GET 分支。
        /// </summary>
        /// <returns>GET 分支文本</returns>
        [HttpGet("routedemo/verbs")]
        public string VerbsGet()
        {
            return "routedemo-verbs-get";
        }

        /// <summary>
        /// 与 VerbsGet 共用同一路径的 POST 分支。
        /// </summary>
        /// <returns>POST 分支文本</returns>
        [HttpPost("routedemo/verbs")]
        public string VerbsPost()
        {
            return "routedemo-verbs-post";
        }

        /// <summary>
        /// 只声明 GET 的路径，用于验证 POST 返回 405。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet("routedemo/getonly")]
        public string GetOnly()
        {
            return "routedemo-getonly-ok";
        }

        /// <summary>
        /// 单段属性路由入口：只有一段路径，不经两段式解析。
        /// </summary>
        /// <returns>固定文本</returns>
        [Route("route-health")]
        public string Health()
        {
            return "routedemo-health-ok";
        }

        /// <summary>
        /// 大小写与路径参数原值入口：静态段比较忽略大小写，参数值保留请求原文。
        /// </summary>
        /// <param name="name">路径中的 {name} 段</param>
        /// <returns>参数原值</returns>
        [HttpGet("routedemo/case/{name}")]
        public string CaseEcho(string name)
        {
            return name;
        }

        /// <summary>
        /// 静态段含空格的入口，用于验证 %20 解码后可命中。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet("routedemo/space/hello world")]
        public string SpaceSegment()
        {
            return "routedemo-space-ok";
        }

        /// <summary>
        /// 带输出缓存的路径参数入口，用于验证不同 id 不共用缓存。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [HttpGet("routedemo/cached/{id}")]
        [OutputCache(300)]
        public string Cached(string id)
        {
            return "routedemo-cached:" + id;
        }
    }
}