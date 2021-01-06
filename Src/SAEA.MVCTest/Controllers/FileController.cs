using SAEA.Common;
using SAEA.MVC;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 文件控制器
    /// </summary>
    public class FileController : Controller
    {
        /// <summary>
        /// 文件输出
        /// </summary>
        /// <returns></returns>
        public ActionResult Download()
        {
            return File(HttpContext.Current.Server.MapPath("/Content/Image/c984b2fb80aeca7b15eda8c004f2e0d4.jpg"));
        }

        /// <summary>
        /// 大数据输出
        /// </summary>
        /// <returns></returns>
        public ActionResult DownloadBigData()
        {
            return BigData(HttpContext.Current.Server.MapPath("/Content/Image/c984b2fb80aeca7b15eda8c004f2e0d4.jpg"));
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Upload(string name)
        {
            var postFiles = HttpContext.Current.Request.PostFiles;

            if (postFiles != null && postFiles.Count > 0)
            {
                var file1 = postFiles[0];

                if (file1 != null)
                {
                    var url = $"/uploads/{DateTimeHelper.ToString("yyyyMMddHHmmssfff")}_{file1.FileName}";

                    file1.Save(HttpContext.Current.Server.MapPath(url));

                    return Content($"上传文件成功！name：{name}，url:{url}");
                }
            }
            return Content("上传文件失败！");
        }

    }
}
