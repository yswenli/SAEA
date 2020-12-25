/****************************************************************************
*项目名称：SAEA.MVCTest.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Controllers
*类 名 称：SaeaSdkTestController
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/25 9:55:32
*描述：
*=====================================================================
*修改时间：2020/12/25 9:55:32
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.MVC;
using SAEA.MVC.Tool.CodeGenerte;
using System.Text;

namespace SAEA.MVCTest.Controllers
{
    public class SaeaSdkTestController : Controller
    {
        public ActionResult GetList()
        {
            var routings = ApiMapping.GetMapping();

            StringBuilder sb = new StringBuilder();

            foreach (var routing in routings)
            {
                sb.Append($"<div><a href='javascript:;' onclick='new SaeaApiSdk().{routing.Instance.GetType().Name.Replace("Controller", "")}{routing.ActionName}(function(data){{alert(data);}},function(e){{alert(e);}})'>/api/{routing.Instance.GetType().Name.Replace("Controller", "")}/{routing.ActionName}</a></div>");
            }

            return Content(sb.ToString());
        }
    }
}
