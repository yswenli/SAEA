using SAEA.MVC;
using SAEA.MVCTest.Model;

namespace SAEA.MVCTest.Controllers
{
    public class AjaxController : Controller
    {
        public ActionResult Test(string str)
        {
            if (IsAjaxRequest())
            {
                return Content($"str={str}. \r\nthis is an ajax request!");
            }
            else
            {
                return Content($"str={str}. \r\nthis is not an ajax request!");
            }
        }


        public ActionResult Test2(string str)
        {
            if (IsAjaxRequest())
            {
                return Content($"str={str}. \r\nthis is an ajax request!");
            }
            else
            {
                return Content($"str={str}. \r\nthis is not an ajax request!");
            }
        }


        public ActionResult Test3(UserInfo userInfo)
        {
            return Json(userInfo);
        }


    }
}
