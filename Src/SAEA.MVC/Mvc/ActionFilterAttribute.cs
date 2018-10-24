/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Mvc
*文件名： ActionFilterAttribute
*版本号： V2.2.2.0
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/11 13:39:02
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/11 13:39:02
*修改人： yswenli
*版本号： V2.2.2.0
*描述：
*
*****************************************************************************/
using SAEA.MVC.Http;
using System;

namespace SAEA.MVC.Mvc
{
    /// <summary>
    /// 拦截器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class ActionFilterAttribute : Attribute
    {
        bool _isEnabled = true;

        /// <summary>
        /// 拦截器
        /// </summary>
        /// <param name="isEnabled"></param>
        public ActionFilterAttribute(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        /// <summary>
        /// 方法执行前
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public abstract bool OnActionExecuting(HttpContext httpContext);

        /// <summary>
        /// 方法执行后
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="result"></param>
        public abstract void OnActionExecuted(HttpContext httpContext, ActionResult result);
    }
}
