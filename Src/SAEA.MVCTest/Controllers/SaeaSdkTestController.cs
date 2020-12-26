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
using System.Linq;
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
                var pas = "";
                if (routing.ParmaTypes != null && routing.ParmaTypes.Any())
                {
                    foreach (var item in routing.ParmaTypes)
                    {
                        if (item.Value.IsClass && !item.Value.IsSealed)
                        {
                            var ppts = item.Value.GetProperties();
                            foreach (var ppt in ppts)
                            {
                                if (ppt.PropertyType == typeof(string))
                                    pas += ("'',");
                                else
                                    pas += ("0,");
                            }
                        }
                        else
                        {
                            if (item.Value == typeof(string))
                                pas += ("'',");
                            else
                                pas += ("0,");
                        }
                    }
                    sb.Append($"<div><a href='javascript:;' onclick=\"new SaeaApiSdk().{routing.ControllerName.Replace("Controller", "")}{routing.ActionName}{(routing.IsPost ? "Post" : "Get")}({pas}function(data){{alert(data);}},function(e){{alert(e);}})\">/api/{routing.ControllerName.Replace("Controller", "")}/{routing.ActionName}</a></div>");
                }
                else
                {
                    sb.Append($"<div><a href='javascript:;' onclick='new SaeaApiSdk().{routing.ControllerName.Replace("Controller", "")}{routing.ActionName}{(routing.IsPost ? "Post" : "Get")}(function(data){{alert(data);}},function(e){{alert(e);}})'>/api/{routing.ControllerName.Replace("Controller", "")}/{routing.ActionName}</a></div>");
                }

            }

            return Content(sb.ToString());
        }
    }
}
