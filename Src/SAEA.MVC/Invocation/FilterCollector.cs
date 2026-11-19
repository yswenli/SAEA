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
*命名空间：SAEA.MVC.Invocation
*文件名： FilterCollector
*版本号： v26.11.16.1
*唯一标识：53546d37-9234-40f7-877f-d3e875b71076
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 13:48:38
*描述：控制器类与 action 上的过滤器收集
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 13:55:49
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增并移入 Invocation/，承载类级与方法级过滤器收集
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC.Invocation
{
    /// <summary>
    /// 过滤器收集：把控制器类与 action 上声明的过滤器按 Order 升序整理，只收集不执行
    /// </summary>
    internal static class FilterCollector
    {
        /// <summary>
        /// 收集控制器类上声明的过滤器
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <returns>已按 Order 升序的过滤器列表；没有声明过滤器时返回 null</returns>
        internal static List<IFilter> CollectClassFilters(Type controllerType)
        {
            return Collect(controllerType.GetCustomAttributes(true));
        }

        /// <summary>
        /// 收集 action 方法上声明的过滤器
        /// </summary>
        /// <param name="action">目标 action 方法</param>
        /// <returns>已按 Order 升序的过滤器列表；没有声明过滤器时返回 null</returns>
        internal static List<IFilter> CollectActionFilters(MethodInfo action)
        {
            return Collect(action.GetCustomAttributes(true));
        }

        /// <summary>
        /// 从特性集合中筛出过滤器并按 Order 升序
        /// </summary>
        /// <param name="attributes">类或方法上取到的特性集合</param>
        /// <returns>已按 Order 升序的过滤器列表；没有过滤器特性时返回 null</returns>
        static List<IFilter> Collect(object[] attributes)
        {
            if (attributes == null || attributes.Length == 0) return null;

            // 过滤器按基类名识别，兼容声明在其它程序集里的 ActionFilterAttribute 派生类
            var filters = attributes
                .Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE)
                .ToList()
                .ConvertAll(b => b as IFilter);

            if (filters == null || filters.Count == 0) return null;

            return filters.OrderBy(b => b.Order).ToList();
        }
    }
}