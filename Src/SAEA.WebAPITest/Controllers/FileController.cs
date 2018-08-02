using SAEA.WebAPI.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.WebAPITest.Controllers
{
    /// <summary>
    /// 文件控制器
    /// </summary>
    public class FileController : APIController
    {
        /// <summary>
        /// 文件输出
        /// </summary>
        /// <returns></returns>
        public ActionResult Download()
        {
            return File(HttpContext.Server.MapPath("/Content/Image/c984b2fb80aeca7b15eda8c004f2e0d4.jpg"));
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Upload(string name)
        {
            var postFiles = HttpContext.Request.PostFiles;

            return Content($"操作成功！name：{name}");
        }

    }
}
