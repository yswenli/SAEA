/****************************************************************************
*项目名称：SAEA.MVC.Tool.CodeGenerte
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC.Tool.CodeGenerte
*类 名 称：ApiMapping
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/24 17:07:57
*描述：
*=====================================================================
*修改时间：2020/12/24 17:07:57
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
