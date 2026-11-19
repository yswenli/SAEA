/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： RouteAttribute
*版本号： v26.11.16.1
*唯一标识：3f9c2a61-7d4e-4b8a-9c15-2e6d8f0a1b73
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 16:35:00
*描述：路由模板特性
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 16:35:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增属性路由
*
*****************************************************************************/
using System;

namespace SAEA.MVC
{
    /// <summary>
    /// 声明控制器或 action 的路由模板
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RouteAttribute : Attribute
    {
        /// <summary>
        /// 以路由模板创建特性
        /// </summary>
        /// <param name="template">路由模板，如 api/users/{id}</param>
        public RouteAttribute(string template)
        {
            Template = template;
        }

        /// <summary>
        /// 声明的路由模板原文
        /// </summary>
        public string Template { get; }
    }
}