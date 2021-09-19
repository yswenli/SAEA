/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： ActionFilterAttribute
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.MVC
{
    /// <summary>
    /// 拦截器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class ActionFilterAttribute : Attribute, IFilter
    {
        /// <summary>
        /// 执行顺序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 方法执行前
        /// </summary>
        /// <returns></returns>
        public abstract ActionResult OnActionExecuting();

        /// <summary>
        /// 方法执行后
        /// </summary>
        /// <param name="result"></param>
        public abstract void OnActionExecuted(ref ActionResult result);
    }
}
