/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Mvc
*文件名： RouteTable
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.WebAPI.Mvc
{
    /// <summary>
    /// SAEA.WebAPI路由表
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
                var list = _list.Where(b => string.Compare(b.ControllerName, controllerName, true) > -1 && string.Compare(b.ActionName, actionName) > -1).ToList();

                if (list == null || list.Count == 0)
                {
                    var actions = controllerType.GetMethods().Where(b => string.Compare(b.Name, actionName, true) > -1).ToList();

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
                        var attrs = controllerType.GetCustomAttributes(true);

                        if (attrs != null && attrs.Length > 0)
                        {
                            var actionAttrs = attrs.Where(b => b.GetType().BaseType.Name == "ActionFilterAttribute").ToList();

                            if (actionAttrs != null && actionAttrs.Count > 0)

                                iAttrs = actionAttrs;

                        }

                        foreach (var action in actions)
                        {
                            var routing = new Routing()
                            {
                                ControllerName = controllerName,
                                ActionName = actionName,
                                Instance = (APIController)instance,
                                FilterAtrrs = iAttrs,
                                Action = action,
                                ActionInvoker = FastInvoke.GetMethodInvoker(action)
                            };

                            //action上面的过滤
                            var actionAttrs = action.GetCustomAttributes(true);

                            if (actionAttrs != null)
                            {
                                var dPost = actionAttrs.Where(b => b.GetType().Name == "HttpPost").FirstOrDefault();
                                if (dPost != null)
                                {
                                    routing.IsPost = true;
                                }
                                else
                                {
                                    routing.IsPost = false;
                                }

                                var filterAttrs = attrs.Where(b => b.GetType().BaseType.Name == "ActionFilterAttribute").ToList();

                                if (filterAttrs != null && filterAttrs.Count > 0)

                                    routing.ActionFilterAtrrs = filterAttrs;
                            }

                            _list.Add(routing);
                        }
                        return _list.Where(b => string.Compare(b.ControllerName, controllerName, true) > -1 && string.Compare(b.ActionName, actionName) > -1 && b.IsPost == isPost).FirstOrDefault();
                    }
                }
                return list.FirstOrDefault(b => b.IsPost == isPost);
            }
        }
    }

}
