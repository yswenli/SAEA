/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net462
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVCTest.Controllers
*文件名： RouteDemoController
*版本号： net462
*唯一标识：82607c56-8012-4f68-a42d-0a57fef1cf03
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 20:30:00
*描述：属性路由演示控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 20:30:00
*修改人： yswenli
*版本号： net462
*描述：新增属性路由演示
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 属性路由演示控制器：类级前缀 routedemo 与方法级模板拼接后即为完整路径
    /// </summary>
    [Route("routedemo")]
    public class RouteDemoController : Controller
    {
        /// <summary>
        /// 多段静态路径，演示类级前缀与方法级模板拼接
        /// </summary>
        /// <returns>命中的完整路径说明</returns>
        [Route("api/v1/users/list")]
        public ActionResult ListUsers()
        {
            return Content("命中多段静态路径：[Route(\"routedemo\")] + [Route(\"api/v1/users/list\")] 拼接为 /routedemo/api/v1/users/list");
        }

        /// <summary>
        /// 单段路径参数，演示路径参数覆盖同名 query
        /// </summary>
        /// <param name="id">来自路径段的 id</param>
        /// <returns>路径参数与原始 query 的对照文本</returns>
        [Route("users/{id}")]
        public ActionResult GetUser(string id)
        {
            var query = HttpContext.Current.Request.Query;

            string queryId;

            if (!query.TryGetValue("id", out queryId)) queryId = "(未提供)";

            return Content($"路径参数 id={id}；同名 query id={queryId}（路径参数优先，query 已被覆盖）");
        }

        /// <summary>
        /// 与 SamePost 共用同一路径的 GET 分支
        /// </summary>
        /// <returns>带 POST 表单的页面片段</returns>
        [HttpGetAttribute("same")]
        public ActionResult SameGet()
        {
            return Content("<h3>GET /routedemo/same 命中 SameGet</h3>"
                + "<p>同一路径的 POST 分支是 SamePost，可直接提交下面的表单：</p>"
                + "<form action=\"/routedemo/same\" method=\"post\">"
                + "<p>note: <input type=\"text\" name=\"note\" /></p>"
                + "<input type=\"submit\" value=\"POST /routedemo/same\" />"
                + "</form>");
        }

        /// <summary>
        /// 与 SameGet 共用同一路径的 POST 分支
        /// </summary>
        /// <param name="note">表单提交的说明文本，缺省时为空字符串</param>
        /// <returns>命中的动词与表单值</returns>
        [HttpPostAttribute("same")]
        public ActionResult SamePost(string note)
        {
            return Content($"POST /routedemo/same 命中 SamePost，note:{note}");
        }

        /// <summary>
        /// 只用 [Route] 声明路径，不限请求方法
        /// </summary>
        /// <returns>本次请求方法</returns>
        [Route("any")]
        public ActionResult AnyVerb()
        {
            return Content($"命中不限动词路由 /routedemo/any，本次请求方法：{HttpContext.Current.Request.Method}");
        }

        /// <summary>
        /// 只允许 GET 的路径，用于观察 POST 时的 405 与 Allow 响应头
        /// </summary>
        /// <returns>提示文本</returns>
        [HttpGetAttribute("only-get")]
        public ActionResult OnlyGet()
        {
            return Content("命中仅允许 GET 的路径 /routedemo/only-get；用 POST 访问应得到 405 且响应头 Allow: GET, HEAD");
        }
    }
}