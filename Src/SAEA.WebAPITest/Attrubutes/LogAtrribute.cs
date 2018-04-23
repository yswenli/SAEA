/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPITest.Common
*文件名： LogAtrribute
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.WebAPI.Http;
using SAEA.WebAPI.Mvc;

namespace SAEA.WebAPITest.Attrubutes
{
    public class LogAtrribute : ActionFilterAttribute
    {
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
            ConsoleHelper.WriteLine($"1请求地址：{httpContext.Request.Query},回复内容：{result.Content}");
        }
    }
    public class Log2Atrribute : ActionFilterAttribute
    {
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
            ConsoleHelper.WriteLine($"2请求地址：{httpContext.Request.Query},回复内容：{result.Content}");
        }
    }
}
