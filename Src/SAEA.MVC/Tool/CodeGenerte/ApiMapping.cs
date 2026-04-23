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
*命名空间：SAEA.MVC.Tool.CodeGenerte
*文件名： ApiMapping
*版本号： v26.4.23.1
*唯一标识：b3eece84-14d5-479a-b6f0-1be230f15e58
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/25 17:36:47
*描述：ApiMapping接口
*
*=====================================================================
*修改标记
*修改时间：2020/12/25 17:36:47
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ApiMapping接口
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC.Tool.CodeGenerte
{
    /// <summary>
    /// saea.mvc api mapping
    /// </summary>
    public static class ApiMapping
    {
        static AreaCollection _areaCollection;

        /// <summary>
        /// saea.mvc api mapping
        /// </summary>
        static ApiMapping()
        {
            _areaCollection = new AreaCollection();
        }

        /// <summary>
        /// 获取映射
        /// </summary>
        /// <returns></returns>
        public static List<Routing> GetMapping()
        {
            Dictionary<string, Routing> dic = new Dictionary<string, Routing>();

            _areaCollection.RegistAll();

            var types = RouteTable.Types;

            foreach (var item in types)
            {
                var instance = Activator.CreateInstance(item);

                List<IFilter> iAttrs = null;

                //类上面的过滤
                var classAttrs = item.GetCustomAttributes(true);

                if (classAttrs != null && classAttrs.Length > 0)
                {
                    var actionAttrs = classAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList().ConvertAll(b => b as IFilter);

                    if (actionAttrs != null && actionAttrs.Count > 0)

                        iAttrs = actionAttrs.OrderBy(b => b.Order).ToList();
                }

                var actions = item.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                foreach (var action in actions)
                {
                    var getRouting = new Routing()
                    {
                        ControllerName = item.Name,
                        ActionName = action.Name,
                        Instance = (Controller)instance,
                        FilterAtrrs = iAttrs,
                        Action = action,
                        IsPost = false
                    };

                    var postRouting = new Routing()
                    {
                        ControllerName = item.Name,
                        ActionName = action.Name,
                        Instance = (Controller)instance,
                        FilterAtrrs = iAttrs,
                        Action = action,
                        IsPost = true
                    };

                    var pas = action.GetParameters();

                    if (pas != null && pas.Any())
                    {
                        getRouting.ParmaTypes = new Dictionary<string, Type>();
                        postRouting.ParmaTypes = new Dictionary<string, Type>();
                        foreach (var pa in pas)
                        {
                            getRouting.ParmaTypes.Add(pa.Name, pa.ParameterType);
                            postRouting.ParmaTypes.Add(pa.Name, pa.ParameterType);
                        }
                    }


                    //action上面的过滤
                    var actionAttrs = action.GetCustomAttributes(true);

                    if (actionAttrs != null && actionAttrs.Length > 0)
                    {
                        var filterAttrs = actionAttrs.Where(b => b.GetType().BaseType.Name == ConstHelper.ACTIONFILTERATTRIBUTE).ToList();

                        if (filterAttrs != null && filterAttrs.Count > 0)
                        {
                            var ifilters = actionAttrs.ToList().ConvertAll(b => b as IFilter);

                            if (ifilters != null && ifilters.Any())
                            {
                                getRouting.ActionFilterAtrrs = postRouting.ActionFilterAtrrs = ifilters.OrderBy(b => b.Order).ToList();

                                var dPost = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPPOST).FirstOrDefault();
                                if (dPost != null)
                                {
                                    dic[postRouting.ControllerName + postRouting.ActionName + (postRouting.IsPost ? "POST" : "GET")] = postRouting;
                                }
                                var dGet = actionAttrs.Where(b => b.GetType().Name == ConstHelper.HTTPGET).FirstOrDefault();
                                if (dGet != null)
                                {
                                    dic[getRouting.ControllerName + getRouting.ActionName + (getRouting.IsPost ? "POST" : "GET")] = getRouting;
                                }
                            }
                            else
                            {
                                dic[getRouting.ControllerName + getRouting.ActionName + (getRouting.IsPost ? "POST" : "GET")] = getRouting;
                                dic[postRouting.ControllerName + postRouting.ActionName + (postRouting.IsPost ? "POST" : "GET")] = postRouting;
                            }
                        }
                        else
                        {
                            dic[getRouting.ControllerName + getRouting.ActionName + (getRouting.IsPost ? "POST" : "GET")] = getRouting;
                            dic[postRouting.ControllerName + postRouting.ActionName + (postRouting.IsPost ? "POST" : "GET")] = postRouting;
                        }
                    }
                    else
                    {
                        dic[getRouting.ControllerName + getRouting.ActionName + (getRouting.IsPost ? "POST" : "GET")] = getRouting;
                        dic[postRouting.ControllerName + postRouting.ActionName + (postRouting.IsPost ? "POST" : "GET")] = postRouting;
                    }
                }
            }
            return dic.Values.ToList();
        }
    }
}
