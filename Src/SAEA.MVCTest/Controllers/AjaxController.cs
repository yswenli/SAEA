using SAEA.MVC;
using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
