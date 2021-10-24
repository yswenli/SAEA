/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： RouteTable
*版本号： v7.0.0.1
*唯一标识：1ed5d381-d7ce-4ea3-b8b5-c32f581ad49f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 10:55:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 10:55:31
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
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

        public static List<Type> Types { get; set; } = new List<Type>();

        /// <summary>
        /// 获取routing中的缓存
        /// 若不存在则创建
        /// </summary>
        /// <param name="controllerType"></param>
        /// <param name="actionName"></param>
        /// <param name="isPost"></param>
        /// <returns></returns>
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
                    var actions = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Where(b => string.Compare(b.Name, actionName, true) == 0).ToList();

                    if (actions == null || !actions.Any())
                    {
                        throw new Exception($"{controllerType.Name}/{actionName}找不到此action!");
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
                                    var filters = actionAttrs.ToList().ConvertAll(b => b as IFilter);

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
                    var actions = controllerType.GetMethods().Where(b => string.Compare(b.Name, actionName, true) == 0).ToList();

                    if (actions == null || actions.Count == 0)
                    {
                        throw new Exception($"{controllerType.Name}/{actionName}找不到此action!");
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
    }

}
