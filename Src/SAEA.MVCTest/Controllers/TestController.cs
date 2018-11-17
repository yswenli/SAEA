using SAEA.MVC;
using SAEA.MVCTest.Model;

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
        public ActionResult Test()
        {
            return Content("this is a test!");
        }

        
        public ActionResult Other(UserInfo userInfo)
        {
            return Json(userInfo);
        }

    }
}
