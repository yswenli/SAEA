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
*文件名： BasicController
*版本号： net10.0
*唯一标识：76141f67-82d8-4a77-8339-4d355a9d6c10
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：普通 HTTP 路由用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:05:27
*修改人： yswenli
*版本号： net10.0
*描述：新增，为 GET/POST/JSON/过滤器/静态文件用例提供路由目标
*
*****************************************************************************/
using SAEA.MVC.Tests.Attributes;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 普通 HTTP 动作的控制器：GET 文本、POST 表单绑定、JSON 结果与过滤器入口。
    /// </summary>
    public class BasicController : Controller
    {
        /// <summary>
        /// GET 入口，返回固定文本。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute]
        public string Index()
        {
            return "basic-index-ok";
        }

        /// <summary>
        /// POST 表单入口，回显绑定到的 name 参数。
        /// </summary>
        /// <param name="name">表单里的 name 字段</param>
        /// <returns>带参数值的回显文本</returns>
        [HttpPostAttribute]
        public string Echo(string name)
        {
            return "basic-echo:" + name;
        }

        /// <summary>
        /// GET 入口，返回 JSON 结果。
        /// </summary>
        /// <returns>固定结构的 JSON 结果</returns>
        [HttpGetAttribute]
        public JsonResult JsonData()
        {
            return Json(new { name = "saea", value = 42 });
        }

        /// <summary>
        /// GET 入口，带记录顺序的过滤器。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute]
        [OrderRecordingFilter]
        public string Filtered()
        {
            OrderRecordingFilterAttribute.Records.Add("action");

            return "filtered-ok";
        }
    }
}