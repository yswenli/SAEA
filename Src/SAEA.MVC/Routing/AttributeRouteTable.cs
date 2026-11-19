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
*命名空间：SAEA.MVC.Routing
*文件名： AttributeRouteTable
*版本号： v26.11.16.1
*唯一标识：8b1e5c47-2a9d-4f36-b07e-5c3a9d24e816
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 16:35:00
*描述：属性路由表
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:55:14
*修改人： yswenli
*版本号： v26.11.16.1
*描述：动词清单改用 HttpMethodCatalog；HEAD 匹配前先找显式声明 [HttpHead] 的条目，优先于 GET 复用
*
*****************************************************************************/
using SAEA.Common;

using SAEA.MVC.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 属性路由表：构建期解析模板并校验冲突，运行期只读匹配
    /// </summary>
    internal sealed class AttributeRouteTable
    {
        static readonly object _registryLock = new object();

        /// <summary>
        /// 每个 RouteTable 实例一份：随应用销毁，弱引用键不会阻止路由表被回收
        /// </summary>
        static readonly ConditionalWeakTable<RouteTable, AttributeRouteTable> _registry = new ConditionalWeakTable<RouteTable, AttributeRouteTable>();

        /// <summary>
        /// 无参数的模板按归一化路径直接命中，忽略大小写
        /// </summary>
        readonly Dictionary<string, List<AttributeRouteEntry>> _staticRoutes = new Dictionary<string, List<AttributeRouteEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 含参数的模板按段数分桶，桶内静态段多者优先
        /// </summary>
        readonly Dictionary<int, List<AttributeRouteEntry>> _parameterRoutes = new Dictionary<int, List<AttributeRouteEntry>>();

        AttributeRouteTable()
        {
        }

        /// <summary>
        /// 按本次发现的控制器类型构建属性路由表，并挂到指定路由表实例
        /// </summary>
        /// <param name="routeTable">应用的示例路由表，用于承载构建结果</param>
        /// <param name="controllerTypes">本次 RegistAll 发现的控制器类型</param>
        /// <returns>构建出的属性路由表</returns>
        internal static AttributeRouteTable Build(RouteTable routeTable, List<Type> controllerTypes)
        {
            var table = new AttributeRouteTable();

            var conflicts = new Dictionary<string, AttributeRouteEntry>(StringComparer.OrdinalIgnoreCase);

            var order = 0;

            if (controllerTypes != null)
            {
                foreach (var controllerType in controllerTypes)
                {
                    if (controllerType == null) continue;

                    var classRoute = GetAttribute<RouteAttribute>(controllerType);

                    List<string> classSegments = null;

                    if (classRoute != null)
                        classSegments = RouteTemplateParser.SplitTemplate(classRoute.Template, RouteTemplateParser.Describe(controllerType, null));

                    var actions = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                    foreach (var action in actions)
                    {
                        var entry = RouteTemplateParser.CreateEntry(controllerType, action, classSegments, order);

                        if (entry == null) continue;

                        table.Add(entry, conflicts);

                        order++;
                    }
                }
            }

            table.SortParameterRoutes();

            if (routeTable != null)
            {
                lock (_registryLock)
                {
                    _registry.Remove(routeTable);
                    _registry.Add(routeTable, table);
                }
            }

            return table;
        }

        /// <summary>
        /// 取指定路由表实例上的属性路由表，未构建过时返回 null
        /// </summary>
        /// <param name="routeTable">路由表实例</param>
        /// <returns>属性路由表；不存在时为 null</returns>
        internal static AttributeRouteTable From(RouteTable routeTable)
        {
            if (routeTable == null) return null;

            AttributeRouteTable table;

            return _registry.TryGetValue(routeTable, out table) ? table : null;
        }

        /// <summary>
        /// 匹配请求路径与请求方法
        /// </summary>
        /// <param name="relativeUrl">请求的原始 request-target，可带 query</param>
        /// <param name="httpMethod">请求方法，如 GET</param>
        /// <returns>命中或路径命中但动词不符的结果；含非法转义、解码出 / 或完全没有属性路由条目时返回 null，调用方继续两段式</returns>
        internal MatchResult Match(string relativeUrl, string httpMethod)
        {
            var path = RoutePath.NormalizePath(relativeUrl);

            var isDecoded = false;

            // 含转义字符时按段解码重建路径，让静态段与参数段共用同一套解码语义；
            // 任一段解码失败或解码出 / 时整条路径判为不匹配，避免越段
            if (path.IndexOf('%') >= 0)
            {
                string decoded;

                if (!RoutePath.TryDecodePath(path, out decoded)) return null;

                path = decoded;

                isDecoded = true;
            }

            List<AttributeRouteEntry> entries;

            if (_staticRoutes.TryGetValue(path, out entries))
            {
                // HEAD 优先命中显式 [HttpHead]：GET 条目同样接受 HEAD，必须先找单独声明 HEAD 的条目
                if (HttpMethodCatalog.IsHead(httpMethod))
                {
                    for (var i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].DeclaresVerb(ConstHelper.HEAD))
                            return Found(entries[i], path, null);
                    }
                }

                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].AllowsMethod(httpMethod))
                        return Found(entries[i], path, null);
                }

                // 路径有声明但动词不匹配：直接 405，不再回落到两段式或参数路由
                return new MatchResult { AllowHeader = RouteSegmentMatcher.CollectAllow(entries) };
            }

            var segmentCount = RoutePath.CountSegments(path);

            List<AttributeRouteEntry> bucket;

            if (_parameterRoutes.TryGetValue(segmentCount, out bucket))
            {
                // 参数路由的 HEAD 预扫描要重新取段值，命中显式 [HttpHead] 后立即返回，不再看后面的 GET 条目
                if (HttpMethodCatalog.IsHead(httpMethod))
                {
                    for (var i = 0; i < bucket.Count; i++)
                    {
                        var entry = bucket[i];

                        if (!entry.DeclaresVerb(ConstHelper.HEAD)) continue;

                        List<KeyValuePair<string, string>> headValues;

                        if (!RouteSegmentMatcher.TryMatchSegments(entry, path, isDecoded, out headValues)) continue;

                        return Found(entry, path, headValues);
                    }
                }

                List<AttributeRouteEntry> pathMatched = null;

                for (var i = 0; i < bucket.Count; i++)
                {
                    var entry = bucket[i];

                    List<KeyValuePair<string, string>> values;

                    if (!RouteSegmentMatcher.TryMatchSegments(entry, path, isDecoded, out values)) continue;

                    if (entry.AllowsMethod(httpMethod))
                        return Found(entry, path, values);

                    if (pathMatched == null) pathMatched = new List<AttributeRouteEntry>();

                    pathMatched.Add(entry);
                }

                if (pathMatched != null)
                    return new MatchResult { AllowHeader = RouteSegmentMatcher.CollectAllow(pathMatched) };
            }

            return null;
        }

        static MatchResult Found(AttributeRouteEntry entry, string path, List<KeyValuePair<string, string>> values)
        {
            return new MatchResult
            {
                ControllerType = entry.ControllerType,
                ActionName = entry.ActionName,
                Template = entry.Template,
                Path = path,
                Values = values
            };
        }

        /// <summary>
        /// 取指定 action 的属性路由声明，供 SDK 生成器等构建期工具读取
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="action">action 方法</param>
        /// <param name="template">归一化后的绝对模板；未声明属性路由时为 null</param>
        /// <param name="httpMethods">限定的请求方法；null 表示不限定</param>
        /// <returns>该 action 参与属性路由时返回 true</returns>
        internal static bool TryGetDeclaredRoute(Type controllerType, MethodInfo action, out string template, out string[] httpMethods)
        {
            template = null;

            httpMethods = null;

            if (controllerType == null || action == null) return false;

            List<string> classSegments = null;

            var classRoute = GetAttribute<RouteAttribute>(controllerType);

            if (classRoute != null)
                classSegments = RouteTemplateParser.SplitTemplate(classRoute.Template, RouteTemplateParser.Describe(controllerType, null));

            // 走建表时的同一条解析路径：类级前缀拼接、模板归一化与非法模板校验都与运行期一致，不在这里另写一套
            var entry = RouteTemplateParser.CreateEntry(controllerType, action, classSegments, 0);

            if (entry == null) return false;

            template = entry.Template;

            httpMethods = entry.HttpMethods;

            return true;
        }

        void Add(AttributeRouteEntry entry, Dictionary<string, AttributeRouteEntry> conflicts)
        {
            var verbs = entry.HttpMethods == null ? HttpMethodCatalog.All : entry.HttpMethods;

            for (var i = 0; i < verbs.Length; i++)
            {
                var key = entry.Shape + "|" + verbs[i];

                AttributeRouteEntry existing;

                if (conflicts.TryGetValue(key, out existing))
                    throw new InvalidOperationException($"属性路由冲突：路径形状 \"{entry.Shape}\" 与 {verbs[i]} 同时被 {existing.ControllerType.Name}/{existing.ActionName}（模板 \"{existing.Template}\"）和 {entry.ControllerType.Name}/{entry.ActionName}（模板 \"{entry.Template}\"）声明");

                conflicts[key] = entry;
            }

            if (entry.HasParameters)
            {
                List<AttributeRouteEntry> bucket;

                if (!_parameterRoutes.TryGetValue(entry.Segments.Length, out bucket))
                {
                    bucket = new List<AttributeRouteEntry>();
                    _parameterRoutes[entry.Segments.Length] = bucket;
                }

                bucket.Add(entry);
            }
            else
            {
                List<AttributeRouteEntry> list;

                if (!_staticRoutes.TryGetValue(entry.Template, out list))
                {
                    list = new List<AttributeRouteEntry>();
                    _staticRoutes[entry.Template] = list;
                }

                list.Add(entry);
            }
        }

        void SortParameterRoutes()
        {
            foreach (var bucket in _parameterRoutes.Values)
            {
                bucket.Sort(CompareParameterEntry);
            }
        }

        static int CompareParameterEntry(AttributeRouteEntry x, AttributeRouteEntry y)
        {
            var byStatic = y.StaticSegmentCount.CompareTo(x.StaticSegmentCount);

            return byStatic != 0 ? byStatic : x.Order.CompareTo(y.Order);
        }

        internal static T GetAttribute<T>(MemberInfo member) where T : Attribute
        {
            if (member == null) return null;

            var attributes = member.GetCustomAttributes(typeof(T), true);

            return attributes != null && attributes.Length > 0 ? (T)attributes[0] : null;
        }

        /// <summary>
        /// 匹配结果：命中时带控制器、action 与归一化实际路径，未命中动词时只带 Allow 内容
        /// </summary>
        internal sealed class MatchResult
        {
            internal Type ControllerType { get; set; }

            internal string ActionName { get; set; }

            internal string Template { get; set; }

            /// <summary>
            /// 本次命中的归一化实际路径，URL 解码后、尾斜杠与连续斜杠已归一
            /// </summary>
            internal string Path { get; set; }

            internal List<KeyValuePair<string, string>> Values { get; set; }

            internal string AllowHeader { get; set; }

            internal bool IsFound
            {
                get { return ControllerType != null; }
            }
        }
    }
}