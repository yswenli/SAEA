/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVCTest.Common
*文件名： LogAtrribute
*版本号： V3.1.1.0
*唯一标识：2a261731-b8f6-47de-b2e4-aecf2e0e0c0f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/11 13:46:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/11 13:46:42
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Model;
using SAEA.MVC;

namespace SAEA.MVCTest.Attrubutes
{
    /// <summary>
    /// 自定义日志记录
    /// </summary>
    public sealed class LogAtrribute : ActionFilterAttribute
    {
        /// <summary>
        /// 自定义日志记录
        /// </summary>
        /// <param name="isEnabled"></param>
        public LogAtrribute(bool isEnabled = true) : base(isEnabled) { }

        /// <summary>
        /// 执行前
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns>返回值true为继续，false为终止</returns>
        public override bool OnActionExecuting(HttpContext httpContext)
        {
            return true;
        }

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="result"></param>
        public override void OnActionExecuted(HttpContext httpContext, ActionResult result)
        {
            ConsoleHelper.WriteLine($"LogAtrribute请求地址：{httpContext.Request.RelativeUrl},回复内容：{result.Content}");
        }
    }
    /// <summary>
    /// 自定义日志记录
    /// </summary>
    public class Log2Atrribute : ActionFilterAttribute
    {
        /// <summary>
        /// 自定义日志记录
        /// </summary>
        /// <param name="isEnabled"></param>
        public Log2Atrribute(bool isEnabled = true) : base(isEnabled) { }

        /// <summary>
        /// 执行前
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns>返回值true为继续，false为终止</returns>
        public override bool OnActionExecuting(HttpContext httpContext)
        {
            return true;
        }

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="result"></param>
        public override void OnActionExecuted(HttpContext httpContext, ActionResult result)
        {
            ConsoleHelper.WriteLine($"Log2Atrribute请求地址：{httpContext.Request.RelativeUrl},回复内容：{result.Content}");
        }
    }
}
