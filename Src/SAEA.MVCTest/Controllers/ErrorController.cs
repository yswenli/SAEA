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
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;
using System;


namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 异常处理示例控制器，演示未处理异常与过滤器抛出异常两种路径
    /// </summary>
    public class ErrorController : Controller
    {
        /// <summary>
        /// 触发除零异常，用于验证框架对未捕获异常的处理
        /// </summary>
        /// <returns>正常执行时的文本结果，当前必然被异常中断</returns>
        public ActionResult Test1()
        {
            var b = 0;

            return Content($"{1/b}");
        }


        /// <summary>
        /// 空动作，由 ErrorAttribute 在动作执行前后抛异常来验证异常捕获
        /// </summary>
        /// <returns>不会被返回，过滤器会先抛出 NotImplementedException</returns>
        [ErrorAttribute]
        public ActionResult Test2()
        {
            return Empty();
        }
    }

    /// <summary>
    /// 动作前后均抛出未实现异常的过滤器，用于验证异常路径
    /// </summary>
    public class ErrorAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// 动作执行后回调，直接抛出未实现异常
        /// </summary>
        /// <param name="result">动作执行结果</param>
        public override void OnActionExecuted(ref ActionResult result)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 动作执行前回调，直接抛出未实现异常
        /// </summary>
        /// <returns>不会返回，调用即抛出 NotImplementedException</returns>
        public override ActionResult OnActionExecuting()
        {
            throw new NotImplementedException();
        }
    }
}
