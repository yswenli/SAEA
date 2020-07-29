/****************************************************************************
*项目名称：SAEA.MVCTest.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Controllers
*类 名 称：ErrorController
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/29 16:37:35
*描述：
*=====================================================================
*修改时间：2020/7/29 16:37:35
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.MVC;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVCTest.Controllers
{
    public class ErrorController : Controller
    {
        public ActionResult Test1()
        {
            var b = 0;

            return Content($"{1/b}");
        }


        [ErrorAttribute]
        public ActionResult Test2()
        {
            return Empty();
        }
    }

    public class ErrorAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionResult result)
        {
            throw new NotImplementedException();
        }

        public override bool OnActionExecuting()
        {
            throw new NotImplementedException();
        }
    }
}
