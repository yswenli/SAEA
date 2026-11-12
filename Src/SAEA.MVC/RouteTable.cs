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
*修改时间：2026/11/12 19:40:12
*修改人： yswenli
*版本号： v26.11.5.1
*描述：未知 action 由抛异常改为返回 null，交由上层返回 404，并更新 GetOrAdd 注释
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC
{
    /// <summary>
    /// SAEA.MVC路由表
    /// </summary>
    public class RouteTable
    {
        ConcurrentDictionary<string, Routing> _cDic = new ConcurrentDictionary<string, Routing>();

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
        public Routing GetOrAdd(Type controllerType, string actionName, bool isPost)
        {
            if (controllerType == null) return null;

            Routing routing = null;

            var getKey = controllerType.Name + actionName + "_" + ConstHelper.GET;

            var postKey = controllerType.Name + actionName + "_" + ConstHelper.POST;

            if (!isPost)
            {
                routing = _cDic.GetOrAdd(getKey, (k) =>
                {
                    var actions = ResolveActions(controllerType, actionName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                    if (actions == null || !actions.Any())
                    {
                        // 未知 action 属于请求错误，返回 null 交给上层回 404；GetOrAdd 会把 null 作为该键的缓存值，后续请求直接命中
                        return null;
                    }
                    else if (actions.Count > 2)
                    {
                        throw new Exception($"{controllerType.Name}/{actionName}有多个重复的的action!");
                    }
                    else
                    {
                        var instance = Activator.CreateInstance(controllerType);

                        List<IFilter> iAttrs = null;

                        //类上面的过滤
                        var classAttrs = controllerType.GetCustomAttributes(true);

                        if (classAttrs != null && classAttrs.Length > 0)
                        {
                            var actionAttrs = classAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList().ConvertAll(b => b as IFilter);

                            if (actionAttrs != null && actionAttrs.Count > 0)

                                iAttrs = actionAttrs.OrderBy(b => b.Order).ToList();

                        }

                        foreach (var action in actions)
                        {
                            routing = new Routing()
                            {
                                ControllerName= controllerType.Name,
                                ActionName = actionName,
                                Instance = (Controller)instance,
                                FilterAtrrs = iAttrs,
                                Action = action,
                                IsPost = false
                            };

                            var routing2 = new Routing()
                            {
                                ControllerName = controllerType.Name,
                                ActionName = actionName,
                                Instance = (Controller)instance,
                                FilterAtrrs = iAttrs,
                                Action = action,
                                IsPost = true
                            };

                            //action上面的过滤
                            var actionAttrs = action.GetCustomAttributes(true);

                            if (actionAttrs != null && actionAttrs.Length > 0)
                            {
                                var filterAttrs = actionAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList();

                                if (filterAttrs != null && filterAttrs.Count > 0)
                                {
                                    var filters = filterAttrs.ConvertAll(b => b as IFilter);

                                    if (filters != null && filters.Any())
                                    {
                                        routing.ActionFilterAtrrs = routing2.ActionFilterAtrrs = filters.OrderBy(b => b.Order).ToList();

                                        var dPost = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPPOST).FirstOrDefault();
                                        if (dPost != null)
                                        {
                                            _cDic.TryAdd(postKey, routing2);
                                        }

                                        var dGet = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPGET).FirstOrDefault();
                                        if (dGet != null)
                                        {
                                            _cDic.TryAdd(getKey, routing);
                                        }
                                    }
                                    else
                                    {
                                        _cDic.TryAdd(getKey, routing);

                                        _cDic.TryAdd(postKey, routing2);
                                    }
                                }
                                else
                                {
                                    _cDic.TryAdd(getKey, routing);

                                    _cDic.TryAdd(postKey, routing2);
                                }
                            }
                            else
                            {
                                _cDic.TryAdd(getKey, routing);

                                _cDic.TryAdd(postKey, routing2);
                            }
                        }
                        return routing;
                    }
                });
            }
            else
            {
                routing = _cDic.GetOrAdd(postKey, (k) =>
                {
                    var actions = ResolveActions(controllerType, actionName, BindingFlags.Instance | BindingFlags.Public);

                    if (actions == null || actions.Count == 0)
                    {
                        // 未知 action 属于请求错误，返回 null 交给上层回 404；GetOrAdd 会把 null 作为该键的缓存值，后续请求直接命中
                        return null;
                    }
                    else if (actions.Count > 2)
                    {
                        throw new Exception($"{controllerType.Name}/{actionName}有多个重复的的action!");
                    }
                    else
                    {
                        var instance = Activator.CreateInstance(controllerType);

                        List<IFilter> iAttrs = null;

                        //类上面的过滤
                        var classAttrs = controllerType.GetCustomAttributes(true);

                        if (classAttrs != null && classAttrs.Length > 0)
                        {
                            var actionAttrs = classAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList();

                            if (actionAttrs != null && actionAttrs.Count > 0)

                                iAttrs = actionAttrs.ConvertAll(b => b as IFilter).OrderBy(b => b.Order).ToList();

                        }

                        foreach (var action in actions)
                        {
                            routing = new Routing()
                            {
                                ActionName = actionName,
                                ControllerName = controllerType.Name,
                                Instance = (Controller)instance,
                                FilterAtrrs = iAttrs,
                                Action = action,
                                IsPost = true
                            };

                            var routing2 = new Routing()
                            {
                                ControllerName = controllerType.Name,
                                ActionName = actionName,
                                Instance = (Controller)instance,
                                FilterAtrrs = iAttrs,
                                Action = action,
                                IsPost = false
                            };
                            //action上面的过滤
                            var actionAttrs = action.GetCustomAttributes(true);

                            if (actionAttrs != null && actionAttrs.Length > 0)
                            {
                                var filterAttrs = actionAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList();

                                if (filterAttrs != null && filterAttrs.Count > 0)

                                    routing.ActionFilterAtrrs = routing2.ActionFilterAtrrs = filterAttrs.ToList().ConvertAll(b => b as IFilter).OrderBy(b => b.Order).ToList();


                                var dGet = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPGET).FirstOrDefault();
                                if (dGet != null)
                                {
                                    _cDic.TryAdd(getKey, routing2);
                                }

                                var dPost = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPPOST).FirstOrDefault();
                                if (dPost != null)
                                {
                                    _cDic.TryAdd(postKey, routing);
                                }
                            }
                            else
                            {
                                _cDic.TryAdd(getKey, routing2);

                                _cDic.TryAdd(postKey, routing);
                            }
                        }
                        return routing;
                    }
                });
            }

            return routing;
        }

        /// <summary>
        /// 解析 action 方法。WSController 的入口方法名带 Async 后缀（OpenAsync），
        /// 路由地址保持 /{controller}/open；GET 与 POST 必须用同一套回退规则，
        /// 否则 POST /chat/open 会查不到 action 而抛 500
        /// </summary>
        static List<MethodInfo> ResolveActions(Type controllerType, string actionName, BindingFlags flags)
        {
            var actions = controllerType.GetMethods(flags)
                .Where(b => string.Compare(b.Name, actionName, true) == 0).ToList();

            if (actions.Count > 0) return actions;

            if (!typeof(WSController).IsAssignableFrom(controllerType)) return actions;

            var asyncName = actionName + "Async";

            actions = controllerType.GetMethods(flags)
                .Where(b => string.Compare(b.Name, asyncName, true) == 0).ToList();

            // WSController 派生类未 override OpenAsync 时，回落到基类声明的那一层
            if (actions.Count == 0)
            {
                actions = typeof(WSController)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(b => string.Compare(b.Name, asyncName, true) == 0).ToList();
            }

            return actions;
        }
    }

}