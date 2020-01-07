using SAEA.MVC;
using SAEA.MVCTest.Attrubutes;
using SAEA.MVCTest.Model;
using System.Threading;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// test
    /// </summary>
    public class TestController: Controller
    {
        /// <summary>
        /// test
        /// </summary>
        /// <returns></returns>
        [AuthAttribute(true)]
        public ActionResult Test()
        {
            return Content("this is a test!");
        }

        [HttpGet]
        [HttpPost]
        public ActionResult Other(UserInfo userInfo)
        {
            return Json(userInfo);
        }

        public ActionResult Timeout()
        {
            int s = 200 * 1000;
            Thread.Sleep(s);
            return Content($"已成功等待{s}毫秒！");
        }

    }
}
