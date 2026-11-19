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
*修改时间：2026/11/19 16:15:31
*修改人： yswenli
*版本号： v26.11.16.1
*描述：GetDeclaredVerbs 改为复用 VerbPolicy.GetAllowedMethods，识别全部 7 个谓词特性并改为 internal 供单测调用
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Invocation;
using SAEA.MVC.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC.Tool.CodeGenerte
{
    /// <summary>
    /// 扫描控制器并生成 action 路由映射
    /// </summary>
    public static class ApiMapping
    {
        static AreaCollection _areaCollection;

        /// <summary>
        /// 静态构造函数，初始化区域控制器集合
        /// </summary>
        static ApiMapping()
        {
            _areaCollection = new AreaCollection();
        }

        /// <summary>
        /// 获取映射
        /// </summary>
        /// <returns></returns>
        public static List<RouteEntry> GetMapping()
        {
            Dictionary<string, RouteEntry> dic = new Dictionary<string, RouteEntry>();

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
                    var getRouting = new RouteEntry()
                    {
                        ControllerName = item.Name,
                        ActionName = action.Name,
                        Instance = (Controller)instance,
                        FilterAtrrs = iAttrs,
                        Action = action,
                        IsPost = false
                    };

                    var postRouting = new RouteEntry()
                    {
                        ControllerName = item.Name,
                        ActionName = action.Name,
                        Instance = (Controller)instance,
                        FilterAtrrs = iAttrs,
                        Action = action,
                        IsPost = true
                    };

                    //属性路由声明：模板与限定动词，与运行期建表共用同一套解析与归一化
                    string routeTemplate;
                    string[] routeMethods;

                    if (AttributeRouteTable.TryGetDeclaredRoute(item, action, out routeTemplate, out routeMethods))
                    {
                        getRouting.Template = postRouting.Template = routeTemplate;
                    }
                    else
                    {
                        // 两段式也受动词特性限定：运行期对只声明 [HttpGet] 的 action 收到 POST 会返回 405，
                        // 这里再生成一条 POST 方法等于给调用方一个必然失败的接口。模板保持 null，地址形态不变
                        routeMethods = GetDeclaredVerbs(action);
                    }

                    getRouting.HttpMethods = postRouting.HttpMethods = routeMethods;

                    // 是否产出某条 sdk 方法只看限定的动词：IsPost 只是两条路由的固定标识（false / true），
                    // 不能拿它判断动词是否被允许。两个 bool 在此算好后所有插入点复用
                    var produceGet = AllowsMethod(routeMethods, ConstHelper.GET);

                    var producePost = AllowsMethod(routeMethods, ConstHelper.POST);

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
                            // 只转换已过滤的列表：action 上可能挂着 [Route]、[WebSocket] 等非过滤器特性，
                            // 全量转换会得到 null 元素并在下面的 OrderBy 里空引用
                            var ifilters = filterAttrs.ConvertAll(b => b as IFilter);

                            if (ifilters != null && ifilters.Any())
                                getRouting.ActionFilterAtrrs = postRouting.ActionFilterAtrrs = ifilters.OrderBy(b => b.Order).ToList();
                        }
                    }

                    // 过滤器不影响是否产出：带 ActionFilterAttribute 但没有动词特性的 action 与无特性时一致，
                    // 按上面算出的 produceGet / producePost 产出
                    AddRouting(dic, getRouting, produceGet);

                    AddRouting(dic, postRouting, producePost);
                }
            }
            return dic.Values.ToList();
        }

        /// <summary>
        /// 判断限定的动词是否允许指定请求方法
        /// </summary>
        /// <param name="httpMethods">限定的请求方法；null 表示不限动词</param>
        /// <param name="method">待判断的请求方法</param>
        /// <returns>允许该方法或未限定动词时返回 true</returns>
        static bool AllowsMethod(string[] httpMethods, string method)
        {
            if (httpMethods == null) return true;

            for (var i = 0; i < httpMethods.Length; i++)
            {
                if (string.Compare(httpMethods[i], method, true) == 0) return true;
            }

            return false;
        }

        /// <summary>
        /// 取 action 上声明的动词特性，供两段式路由判定允许的请求方法
        /// </summary>
        /// <param name="action">action 方法</param>
        /// <returns>按 HttpMethodCatalog.All 顺序去重后的动词数组；未声明动词特性时为 null，表示不限谓词</returns>
        internal static string[] GetDeclaredVerbs(MethodInfo action)
        {
            // 与运行期 405 判定共用同一口径：认全部谓词特性，避免为只声明新谓词的 action 生成必然 405 的 GET/POST
            return VerbPolicy.GetAllowedMethods(action);
        }

        /// <summary>
        /// 按动词过滤结果把路由写入映射表
        /// </summary>
        /// <param name="dic">映射表，键为控制器名 + action 名 + 动词</param>
        /// <param name="routing">待写入的路由</param>
        /// <param name="allowed">是否允许产出该条路由</param>
        static void AddRouting(Dictionary<string, RouteEntry> dic, RouteEntry routing, bool allowed)
        {
            if (!allowed) return;

            dic[routing.ControllerName + routing.ActionName + (routing.IsPost ? "POST" : "GET")] = routing;
        }
    }
}
