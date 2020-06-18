/****************************************************************************
*项目名称：SAEA.MVCTest.Attrubutes
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Attrubutes
*类 名 称：AuthAttribute
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/1/7 18:21:46
*描述：
*=====================================================================
*修改时间：2020/1/7 18:21:46
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.MVC;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVCTest.Attrubutes
{
    public class AuthAttribute : ActionFilterAttribute
    {
        public AuthAttribute(bool isEnabled) : base(isEnabled)
        {

        }

        public override void OnActionExecuted(ActionResult result)
        {
            
        }

        public override bool OnActionExecuting()
        {
            HttpContext.Current.Response.SetCached(new JsonResult("当前操作需要登录！"));
            HttpContext.Current.Response.End();
            return false;
        }
    }
}
