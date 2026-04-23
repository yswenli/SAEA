/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： ActionFilterAttribute
*版本号： v26.4.23.1
*唯一标识：4978b882-2c96-45b6-bc3c-58e40a93ae52
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：ActionFilterAttribute接口
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ActionFilterAttribute接口
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