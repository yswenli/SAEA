/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Mvc
*文件名： RouteTable
*版本号： V2.1.5.2
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
*版本号： V2.1.5.2
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.MVC.Mvc
{
    /// <summary>
    /// SAEA.MVC路由表
    /// </summary>
    public static class RouteTable
    {
        static object _locker = new object();

        static List<Routing> _list = new List<Routing>();


        /// <summary>
        /// 获取routing中的缓存
        /// 若不存在则创建
        /// </summary>
        /// <param name="controllerType"></param>
        /// <param name="controllerName"></param>
        /// <param name="actionName"></param>
        /// <param name="isPost"></param>
        /// <returns></returns>
        public static Routing GetOrAdd(Type controllerType, string controllerName, string actionName, bool isPost)
        {
            lock (_locker)
            {
                var list = _list.Where(b => string.Compare(b.ControllerName, controllerName, true) == 0 && string.Compare(b.ActionName, actionName, true) == 0).ToList();

                if (list == null || list.Count == 0 || list.FirstOrDefault(b => b.IsPost == isPost) == null)
                {
                    var actions = controllerType.GetMethods().Where(b => string.Compare(b.Name, actionName, true) == 0).ToList();

                    if (actions == null || actions.Count == 0)
                    {
                        throw new Exception($"{controllerName}/{actionName}找不到此action!");
                    }
                    else if (actions.Count > 2)
                    {
                        throw new Exception($"{controllerName}/{actionName}有多个重复的的action!");
                    }
                    else
                    {
                        var instance = System.Activator.CreateInstance(controllerType);

                        List<object> iAttrs = null;

                        //类上面的过滤
                        var classAttrs = controllerType.GetCustomAttributes(true);

                        if (classAttrs != null && classAttrs.Length > 0)
                        {
                            var actionAttrs = classAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList();

                            if (actionAttrs != null && actionAttrs.Count > 0)

                                iAttrs = actionAttrs;

                        }

                        foreach (var action in actions)
                        {
                            var routing = new Routing()
                            {
                                ControllerName = controllerName,
                                ActionName = actionName,
                                Instance = (Controller)instance,
                                FilterAtrrs = iAttrs,
                                Action = action,
                                ActionInvoker = FastInvoke.GetMethodInvoker(action)
                            };

                            //action上面的过滤
                            var actionAttrs = action.GetCustomAttributes(true);

                            if (actionAttrs != null && actionAttrs.Length > 0)
                            {
                                var filterAttrs = actionAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList();

                                if (filterAttrs != null && filterAttrs.Count > 0)

                                    routing.ActionFilterAtrrs = actionAttrs.ToList();

                                var dPost = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPPOST).FirstOrDefault();
                                if (dPost != null)
                                {
                                    var dGet = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPGET).FirstOrDefault();
                                    if (dGet != null)
                                    {
                                        routing.IsPost = false;
                                        _list.Add(routing);
                                    }

                                    var routing2 = new Routing()
                                    {
                                        ControllerName = controllerName,
                                        ActionName = actionName,
                                        Instance = (Controller)instance,
                                        FilterAtrrs = iAttrs,
                                        Action = action,
                                        ActionInvoker = FastInvoke.GetMethodInvoker(action),
                                        ActionFilterAtrrs = routing.ActionFilterAtrrs
                                    };
                                    routing2.IsPost = true;
                                    _list.Add(routing2);
                                }
                                else
                                {
                                    routing.IsPost = false;
                                    _list.Add(routing);
                                }
                            }
                            else
                            {
                                routing.IsPost = false;
                                _list.Add(routing);
                            }
                        }
                        return _list.Where(b => string.Compare(b.ControllerName, controllerName, true) == 0 && string.Compare(b.ActionName, actionName, true) == 0 && b.IsPost == isPost).FirstOrDefault();
                    }
                }
                return list.FirstOrDefault(b => b.IsPost == isPost);
            }
        }
    }

}
