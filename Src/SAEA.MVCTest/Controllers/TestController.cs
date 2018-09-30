using SAEA.MVC.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
