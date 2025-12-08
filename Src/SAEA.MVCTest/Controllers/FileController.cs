using SAEA.Common;
using SAEA.Common.IO;
using SAEA.MVC;
using System.Threading.Tasks;

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
            return File(HttpContext.Current.Server.MapPath("/Content/Images/6139455.png"));
        }

        /// <summary>
        /// 大数据输出
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> DownloadBigData()
        {
            await Task.Yield();
            return BigData(HttpContext.Current.Server.MapPath("/Content/Images/6139455.png"));
        }

        /// <summary>
        /// 流处理后输出
        /// </summary>
        /// <returns></returns>
        public ActionResult GetPic()
        {
            return File(HttpContext.Current.Server.MapPath("/Content/Images/6139455.png"));
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
