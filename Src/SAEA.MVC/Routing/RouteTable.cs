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
*命名空间：SAEA.MVC.Routing
*文件名： RouteTable
*版本号： v26.4.23.1
*唯一标识：b12d2d02-7eb0-4743-9168-205c58375de6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：RouteTable类
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：注释与实现对齐：只有未声明动词的 action 才按 All 补写全部动词键，声明了动词的只写当前键；说明当前键的 TryAdd 不能省
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Invocation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// SAEA.MVC路由表
    /// </summary>
    public class RouteTable
    {
        ConcurrentDictionary<string, RouteEntry> _cDic = new ConcurrentDictionary<string, RouteEntry>();

        /// <summary>
        /// 已扫描到的控制器类型集合
        /// </summary>
        public static List<Type> Types { get; set; } = new List<Type>();

        /// <summary>
        /// 获取routing中的缓存
        /// 若不存在则创建
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">动作名称</param>
        /// <param name="isPost">是否为 POST 请求</param>
        /// <returns>匹配到的路由；找不到 action 或 controllerType 为 null 时返回 null</returns>
        public RouteEntry GetOrAdd(Type controllerType, string actionName, bool isPost)
        {
            return GetOrAdd(controllerType, actionName, isPost ? ConstHelper.POST : ConstHelper.GET);
        }

        /// <summary>
        /// 按请求方法获取routing中的缓存
        /// 若不存在则创建
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">动作名称</param>
        /// <param name="method">请求方法，如 GET；为空时按 GET 处理</param>
        /// <returns>匹配到的路由；找不到 action 或 controllerType 为 null 时返回 null</returns>
        public RouteEntry GetOrAdd(Type controllerType, string actionName, string method)
        {
            if (controllerType == null) return null;

            // 未给出方法时按 GET 处理，保持旧的重载与空方法调用等价
            if (string.IsNullOrEmpty(method)) method = ConstHelper.GET;

            var key = controllerType.Name + actionName + "_" + method;

            // 只为当前请求动词的键做一次 GetOrAdd：未声明动词的 action 由工厂补写全部动词的键，
            // 声明了动词的 action 只落回自己声明的键；解析为空时工厂返回 null 并作为该键的缓存值，后续请求直接命中
            return _cDic.GetOrAdd(key, (k) => BuildEntry(controllerType, actionName, method, key));
        }

        /// <summary>
        /// 解析 action 并构造当前动词的路由条目：未声明动词时按 HttpMethodCatalog.All 补写全部动词的键，
        /// 声明了动词时只写当前动词的键
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">动作名称</param>
        /// <param name="method">请求方法，决定 action 的解析范围与条目的动词标记</param>
        /// <param name="key">当前动词的路由缓存键</param>
        /// <returns>当前动词对应的路由条目；找不到 action 时返回 null</returns>
        RouteEntry BuildEntry(Type controllerType, string actionName, string method, string key)
        {
            var actions = ActionResolver.Resolve(controllerType, actionName, method);

            if (actions == null || actions.Count == 0)
            {
                // 未知 action 属于请求错误，返回 null 交给上层回 404；GetOrAdd 会把 null 作为该键的缓存值，后续请求直接命中
                return null;
            }

            if (actions.Count > 2)
            {
                throw new Exception($"{controllerType.Name}/{actionName}有多个重复的的action!");
            }

            // 一次解析只创建一个控制器实例，同一 action 的各个动词条目共用
            var instance = Activator.CreateInstance(controllerType);

            var classFilters = FilterCollector.CollectClassFilters(controllerType);

            RouteEntry entry = null;

            foreach (var action in actions)
            {
                entry = new RouteEntry()
                {
                    ControllerName = controllerType.Name,
                    ActionName = actionName,
                    Instance = (Controller)instance,
                    FilterAtrrs = classFilters,
                    Action = action,
                    IsPost = string.Compare(method, ConstHelper.POST, true) == 0
                };

                var actionFilters = FilterCollector.CollectActionFilters(action);

                if (actionFilters != null)
                {
                    entry.ActionFilterAtrrs = actionFilters;
                }

                var methodAttributes = HttpMethodCatalog.FromAttributes(action.GetCustomAttributes(true));

                if (methodAttributes.Length > 0)
                {
                    // 声明了动词特性时只写命中的键：未声明的动词留给它自己的解析，不提前固化别的动词的条目；
                    // 即使当前动词未被声明也返回非 null 条目，让上层按声明给出 405 而不是 404
                    for (var i = 0; i < methodAttributes.Length; i++)
                    {
                        if (string.Compare(methodAttributes[i].Method, method, true) != 0) continue;

                        // 这次写入与外层 GetOrAdd 落在同一个键上，但不能省：同一 action 名解析出两个重载且都声明
                        // 当前动词时，先遍历到的重载在这里落键，外层 GetOrAdd 不覆盖已有值，覆盖语义由这次写入决定
                        _cDic.TryAdd(key, entry);
                    }
                }
                else
                {
                    // 未声明动词时对全部已知动词放行，按 All 补写缓存键
                    for (var i = 0; i < HttpMethodCatalog.All.Length; i++)
                    {
                        _cDic.TryAdd(controllerType.Name + actionName + "_" + HttpMethodCatalog.All[i], entry);
                    }
                }
            }

            return entry;
        }
    }

}