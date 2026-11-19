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
*修改时间：2026/11/19 11:16:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：文件移入 Routing/ 并改用命名空间 SAEA.MVC.Routing，无逻辑变更
*
*****************************************************************************/
using SAEA.Common;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 属性路由表：构建期解析模板并校验冲突，运行期只读匹配
    /// </summary>
    internal sealed class AttributeRouteTable
    {
        const char Slash = '/';

        const char OpenBrace = '{';

        const char CloseBrace = '}';

        const char OpenBracket = '[';

        const char CloseBracket = ']';

        const string ControllerToken = "[controller]";

        const string ActionToken = "[action]";

        const string ControllerSuffix = "Controller";

        const string ActionSuffix = "Async";

        static readonly string[] AllVerbs = { ConstHelper.GET, ConstHelper.POST, ConstHelper.PUT, ConstHelper.DELETE };

        static readonly object _registryLock = new object();

        /// <summary>
        /// 每个 RouteTable 实例一份：随应用销毁，弱引用键不会阻止路由表被回收
        /// </summary>
        static readonly ConditionalWeakTable<RouteTable, AttributeRouteTable> _registry = new ConditionalWeakTable<RouteTable, AttributeRouteTable>();

        /// <summary>
        /// 无参数的模板按归一化路径直接命中，忽略大小写
        /// </summary>
        readonly Dictionary<string, List<Entry>> _staticRoutes = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 含参数的模板按段数分桶，桶内静态段多者优先
        /// </summary>
        readonly Dictionary<int, List<Entry>> _parameterRoutes = new Dictionary<int, List<Entry>>();

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

            var conflicts = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            var order = 0;

            if (controllerTypes != null)
            {
                foreach (var controllerType in controllerTypes)
                {
                    if (controllerType == null) continue;

                    var classRoute = GetAttribute<RouteAttribute>(controllerType);

                    List<string> classSegments = null;

                    if (classRoute != null)
                        classSegments = SplitTemplate(classRoute.Template, Describe(controllerType, null));

                    var actions = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                    foreach (var action in actions)
                    {
                        var entry = CreateEntry(controllerType, action, classSegments, order);

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
            var path = NormalizePath(relativeUrl);

            var isDecoded = false;

            // 含转义字符时按段解码重建路径，让静态段与参数段共用同一套解码语义；
            // 任一段解码失败或解码出 / 时整条路径判为不匹配，避免越段
            if (path.IndexOf('%') >= 0)
            {
                string decoded;

                if (!TryDecodePath(path, out decoded)) return null;

                path = decoded;

                isDecoded = true;
            }

            List<Entry> entries;

            if (_staticRoutes.TryGetValue(path, out entries))
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].AllowsMethod(httpMethod))
                        return Found(entries[i], path, null);
                }

                // 路径有声明但动词不匹配：直接 405，不再回落到两段式或参数路由
                return new MatchResult { AllowHeader = CollectAllow(entries) };
            }

            var segmentCount = CountSegments(path);

            List<Entry> bucket;

            if (_parameterRoutes.TryGetValue(segmentCount, out bucket))
            {
                List<Entry> pathMatched = null;

                for (var i = 0; i < bucket.Count; i++)
                {
                    var entry = bucket[i];

                    List<KeyValuePair<string, string>> values;

                    if (!TryMatchSegments(entry, path, isDecoded, out values)) continue;

                    if (entry.AllowsMethod(httpMethod))
                        return Found(entry, path, values);

                    if (pathMatched == null) pathMatched = new List<Entry>();

                    pathMatched.Add(entry);
                }

                if (pathMatched != null)
                    return new MatchResult { AllowHeader = CollectAllow(pathMatched) };
            }

            return null;
        }

        static MatchResult Found(Entry entry, string path, List<KeyValuePair<string, string>> values)
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

        #region 构建

        static Entry CreateEntry(Type controllerType, MethodInfo action, List<string> classSegments, int order)
        {
            var route = GetAttribute<RouteAttribute>(action);

            var get = GetAttribute<HttpGetAttribute>(action);

            var post = GetAttribute<HttpPostAttribute>(action);

            var routeTemplate = route == null ? null : route.Template;

            var getTemplate = get == null ? null : get.Template;

            var postTemplate = post == null ? null : post.Template;

            var sources = 0;

            if (route != null) sources++;

            if (getTemplate != null) sources++;

            if (postTemplate != null) sources++;

            var owner = Describe(controllerType, action);

            if (sources > 1)
                throw new ArgumentException($"{owner} 的路径声明不唯一：[Route]、[HttpGet(模板)]、[HttpPost(模板)] 中同时出现了多个带模板的特性");

            var methodTemplate = routeTemplate;

            if (methodTemplate == null) methodTemplate = getTemplate;

            if (methodTemplate == null) methodTemplate = postTemplate;

            var hasVerb = get != null || post != null;

            // 类上只有模板、方法上没有任何路由特性时保持两段式，避免类级模板吞掉全部 action
            if (sources == 0 && !(classSegments != null && hasVerb)) return null;

            var segments = new List<string>();

            if (classSegments != null) segments.AddRange(classSegments);

            if (sources > 0) segments.AddRange(SplitTemplate(methodTemplate, owner));

            // 替换前的模板原文同时用于令牌取值失败的异常消息
            var rawTemplate = BuildTemplate(segments);

            // 类级段列表由同一控制器的多个 action 共享，上面的 AddRange 已把内容拷进本次条目自己的列表，
            // 这里原地替换令牌只会影响当前 action，不会污染其它 action 已算好的路径
            ReplaceTokens(segments, controllerType, action, owner, rawTemplate);

            var verbs = new List<string>();

            if (get != null && post != null)
            {
                verbs.Add(ConstHelper.GET);
                verbs.Add(ConstHelper.POST);
            }
            else if (get != null)
            {
                verbs.Add(ConstHelper.GET);
            }
            else if (post != null)
            {
                verbs.Add(ConstHelper.POST);
            }

            var entry = new Entry
            {
                ControllerType = controllerType,
                ActionName = action.Name,
                Template = BuildTemplate(segments),
                Shape = BuildShape(segments),
                Segments = BuildSegments(segments),
                HttpMethods = verbs.Count > 0 ? verbs.ToArray() : null,
                Order = order
            };

            for (var i = 0; i < entry.Segments.Length; i++)
            {
                if (entry.Segments[i].IsParameter)
                    entry.HasParameters = true;
                else
                    entry.StaticSegmentCount++;
            }

            return entry;
        }

        /// <summary>
        /// 逐 action 替换段里的 [controller] 与 [action] 令牌，类级模板中的令牌因此按每个 action 分别取值
        /// </summary>
        /// <param name="segments">拼接后的段列表，令牌段会被原地替换为实际名称</param>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="action">action 方法</param>
        /// <param name="owner">声明位置描述，仅用于异常消息</param>
        /// <param name="template">替换前的模板原文，仅用于异常消息</param>
        static void ReplaceTokens(List<string> segments, Type controllerType, MethodInfo action, string owner, string template)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];

                if (string.Compare(segment, ControllerToken, true) == 0)
                {
                    segments[i] = TrimSuffix(controllerType.Name, ControllerSuffix, ControllerToken, template, owner);
                }
                else if (string.Compare(segment, ActionToken, true) == 0)
                {
                    segments[i] = TrimSuffix(action.Name, ActionSuffix, ActionToken, template, owner);
                }
            }
        }

        /// <summary>
        /// 去掉一个结尾后缀并校验结果非空，[controller] 与 [action] 共用同一套规则
        /// </summary>
        /// <param name="value">原始名称</param>
        /// <param name="suffix">要剥掉的结尾后缀</param>
        /// <param name="token">令牌原文，仅用于异常消息</param>
        /// <param name="template">模板原文，仅用于异常消息</param>
        /// <param name="owner">声明位置描述，仅用于异常消息</param>
        /// <returns>去掉后缀后的名称</returns>
        static string TrimSuffix(string value, string suffix, string token, string template, string owner)
        {
            if (value.Length >= suffix.Length && string.Compare(value, value.Length - suffix.Length, suffix, 0, suffix.Length, StringComparison.OrdinalIgnoreCase) == 0)
                value = value.Substring(0, value.Length - suffix.Length);

            if (value.Length == 0)
                throw new ArgumentException($"路由模板 \"{template}\" 的令牌 \"{token}\" 无法取值：{owner} 去掉结尾 {suffix} 后缀后为空");

            return value;
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
                classSegments = SplitTemplate(classRoute.Template, Describe(controllerType, null));

            // 走建表时的同一条解析路径：类级前缀拼接、模板归一化与非法模板校验都与运行期一致，不在这里另写一套
            var entry = CreateEntry(controllerType, action, classSegments, 0);

            if (entry == null) return false;

            template = entry.Template;

            httpMethods = entry.HttpMethods;

            return true;
        }

        /// <summary>
        /// 按 / 切分模板并校验每个段，连续 / 与首尾 / 直接忽略
        /// </summary>
        /// <param name="template">模板原文，可为 null 或空</param>
        /// <param name="owner">声明位置描述，仅用于异常消息</param>
        /// <returns>去空段后的段列表</returns>
        static List<string> SplitTemplate(string template, string owner)
        {
            var segments = new List<string>();

            if (string.IsNullOrEmpty(template)) return segments;

            var text = template.Trim();

            var start = 0;

            for (var i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] != Slash) continue;

                if (i > start) segments.Add(text.Substring(start, i - start));

                start = i + 1;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                ValidateSegment(segments[i], owner, template);
            }

            return segments;
        }

        /// <summary>
        /// 只接受纯静态段、纯占位段 {name} 或独占一段的 [controller] / [action] 令牌，其余写法在构建期报错，不做静默降级
        /// </summary>
        /// <param name="segment">单个模板段</param>
        /// <param name="owner">声明位置描述，仅用于异常消息</param>
        /// <param name="template">完整模板原文，仅用于异常消息</param>
        static void ValidateSegment(string segment, string owner, string template)
        {
            if (segment.IndexOf(OpenBracket) >= 0 || segment.IndexOf(CloseBracket) >= 0)
            {
                // 令牌必须整段独占；未知令牌、与字面量混写、未闭合的方括号都按非法模板报错，不能当成静态段静默注册
                if (string.Compare(segment, ControllerToken, true) != 0 && string.Compare(segment, ActionToken, true) != 0)
                    throw new ArgumentException($"路由模板 \"{template}\" 的段 \"{segment}\" 非法：只支持 [controller] 与 [action] 两个令牌，且必须独占一段（{owner}）");

                return;
            }

            if (segment.IndexOf(OpenBrace) < 0 && segment.IndexOf(CloseBrace) < 0) return;

            var isPlaceholder = segment.Length > 2
                && segment[0] == OpenBrace
                && segment[segment.Length - 1] == CloseBrace
                && segment.IndexOf(OpenBrace, 1) < 0
                && segment.IndexOf(CloseBrace) == segment.Length - 1;

            if (!isPlaceholder)
                throw new ArgumentException($"路由模板 \"{template}\" 的段 \"{segment}\" 非法：只支持静态段或纯占位段 {{name}}，不支持可选参数、通配与类型约束（{owner}）");

            for (var i = 1; i < segment.Length - 1; i++)
            {
                var ch = segment[i];

                if (ch == '?' || ch == '*' || ch == ':')
                    throw new ArgumentException($"路由模板 \"{template}\" 的段 \"{segment}\" 非法：不支持可选参数、通配与类型约束（{owner}）");

                var valid = (ch >= 'a' && ch <= 'z')
                    || (ch >= 'A' && ch <= 'Z')
                    || (ch >= '0' && ch <= '9')
                    || ch == '_';

                if (!valid)
                    throw new ArgumentException($"路由模板 \"{template}\" 的占位名 \"{segment}\" 非法：只允许字母、数字与下划线（{owner}）");
            }
        }

        static string BuildTemplate(List<string> segments)
        {
            if (segments.Count == 0) return "/";

            var builder = new StringBuilder();

            for (var i = 0; i < segments.Count; i++)
            {
                builder.Append(Slash);
                builder.Append(segments[i]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 构建路径形状：参数段统一归一化为 {}，用于识别只有占位名不同的等价模板
        /// </summary>
        /// <param name="segments">模板段列表</param>
        /// <returns>形状键，如 /users/{}</returns>
        static string BuildShape(List<string> segments)
        {
            if (segments.Count == 0) return "/";

            var builder = new StringBuilder();

            for (var i = 0; i < segments.Count; i++)
            {
                builder.Append(Slash);

                var text = segments[i];

                if (text.Length > 2 && text[0] == OpenBrace && text[text.Length - 1] == CloseBrace)
                    builder.Append("{}");
                else
                    builder.Append(text);
            }

            return builder.ToString();
        }

        static Segment[] BuildSegments(List<string> segments)
        {
            var result = new Segment[segments.Count];

            for (var i = 0; i < segments.Count; i++)
            {
                var text = segments[i];

                var isParameter = text.Length > 2 && text[0] == OpenBrace && text[text.Length - 1] == CloseBrace;

                result[i] = new Segment
                {
                    IsParameter = isParameter,
                    Value = isParameter ? text.Substring(1, text.Length - 2) : text
                };
            }

            return result;
        }

        void Add(Entry entry, Dictionary<string, Entry> conflicts)
        {
            var verbs = entry.HttpMethods == null ? AllVerbs : entry.HttpMethods;

            for (var i = 0; i < verbs.Length; i++)
            {
                var key = entry.Shape + "|" + verbs[i];

                Entry existing;

                if (conflicts.TryGetValue(key, out existing))
                    throw new InvalidOperationException($"属性路由冲突：路径形状 \"{entry.Shape}\" 与 {verbs[i]} 同时被 {existing.ControllerType.Name}/{existing.ActionName}（模板 \"{existing.Template}\"）和 {entry.ControllerType.Name}/{entry.ActionName}（模板 \"{entry.Template}\"）声明");

                conflicts[key] = entry;
            }

            if (entry.HasParameters)
            {
                List<Entry> bucket;

                if (!_parameterRoutes.TryGetValue(entry.Segments.Length, out bucket))
                {
                    bucket = new List<Entry>();
                    _parameterRoutes[entry.Segments.Length] = bucket;
                }

                bucket.Add(entry);
            }
            else
            {
                List<Entry> list;

                if (!_staticRoutes.TryGetValue(entry.Template, out list))
                {
                    list = new List<Entry>();
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

        static int CompareParameterEntry(Entry x, Entry y)
        {
            var byStatic = y.StaticSegmentCount.CompareTo(x.StaticSegmentCount);

            return byStatic != 0 ? byStatic : x.Order.CompareTo(y.Order);
        }

        static T GetAttribute<T>(MemberInfo member) where T : Attribute
        {
            if (member == null) return null;

            var attributes = member.GetCustomAttributes(typeof(T), true);

            return attributes != null && attributes.Length > 0 ? (T)attributes[0] : null;
        }

        static string Describe(Type controllerType, MethodInfo action)
        {
            return action == null ? controllerType.Name : controllerType.Name + "/" + action.Name;
        }

        #endregion

        #region 路径处理

        /// <summary>
        /// 取 request-target 中 ? 之前的路径并归一化：单前导 /、合并连续 /、无尾斜杠
        /// </summary>
        /// <param name="relativeUrl">原始 request-target</param>
        /// <returns>归一化后的绝对路径</returns>
        static string NormalizePath(string relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return "/";

            var end = relativeUrl.IndexOf('?');

            if (end < 0) end = relativeUrl.Length;

            // 常见路径已规范，只截掉 query，避免每个请求都重建字符串
            var canonical = end > 0 && relativeUrl[0] == Slash;

            if (canonical)
            {
                for (var i = 1; i < end; i++)
                {
                    if (relativeUrl[i] != Slash) continue;

                    if (relativeUrl[i - 1] == Slash || i == end - 1)
                    {
                        canonical = false;
                        break;
                    }
                }
            }

            if (canonical)
                return end == relativeUrl.Length ? relativeUrl : relativeUrl.Substring(0, end);

            return BuildNormalizedPath(relativeUrl, end);
        }

        static string BuildNormalizedPath(string path, int end)
        {
            var builder = new StringBuilder(end + 1);

            builder.Append(Slash);

            // 前导 / 已写入，连续 / 直接跳过
            var lastWasSlash = true;

            for (var i = 0; i < end; i++)
            {
                var ch = path[i];

                if (ch == Slash)
                {
                    if (lastWasSlash) continue;

                    builder.Append(Slash);
                    lastWasSlash = true;
                }
                else
                {
                    builder.Append(ch);
                    lastWasSlash = false;
                }
            }

            if (builder.Length > 1 && builder[builder.Length - 1] == Slash) builder.Length--;

            return builder.ToString();
        }

        /// <summary>
        /// 按段解码含转义的路径并重建；任一段解码失败或解码出 / 时判为不匹配
        /// </summary>
        /// <param name="path">归一化路径</param>
        /// <param name="decoded">解码后的路径，段数保持不变</param>
        /// <returns>可继续匹配时返回 true</returns>
        static bool TryDecodePath(string path, out string decoded)
        {
            decoded = null;

            var builder = new StringBuilder(path.Length);

            builder.Append(Slash);

            var pos = path.Length > 0 && path[0] == Slash ? 1 : 0;

            while (true)
            {
                int start;
                int length;

                if (!TryReadSegment(path, ref pos, out start, out length)) break;

                string value;

                // 解码出 / 说明请求用 %2F 越段，按不匹配处理，不能当成两个段
                if (!TryDecodeSegment(path, start, length, out value)) return false;

                if (builder.Length > 1) builder.Append(Slash);

                builder.Append(value);
            }

            decoded = builder.ToString();

            return true;
        }

        static int CountSegments(string path)
        {
            var count = 0;

            var last = path.Length - 1;

            for (var i = 0; i < last; i++)
            {
                if (path[i] == Slash) count++;
            }

            return count;
        }

        /// <summary>
        /// 从 pos 处读出一个段的边界；归一化路径里不存在空段
        /// </summary>
        /// <param name="path">归一化路径</param>
        /// <param name="pos">读取位置，返回时指向下一段的起点</param>
        /// <param name="start">段首索引</param>
        /// <param name="length">段长度</param>
        /// <returns>读到段返回 true，已到末尾返回 false</returns>
        static bool TryReadSegment(string path, ref int pos, out int start, out int length)
        {
            start = pos;

            length = 0;

            if (pos >= path.Length) return false;

            var index = path.IndexOf(Slash, pos);

            if (index < 0)
            {
                length = path.Length - pos;
                pos = path.Length;
            }
            else
            {
                length = index - pos;
                pos = index + 1;
            }

            return length > 0;
        }

        static bool SegmentEquals(string path, int start, int length, string literal)
        {
            return length == literal.Length && string.Compare(path, start, literal, 0, literal.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        static bool TryMatchSegments(Entry entry, string path, bool alreadyDecoded, out List<KeyValuePair<string, string>> values)
        {
            values = null;

            var segments = entry.Segments;

            var pos = path.Length > 0 && path[0] == Slash ? 1 : 0;

            List<KeyValuePair<string, string>> collected = null;

            for (var i = 0; i < segments.Length; i++)
            {
                int start;
                int length;

                if (!TryReadSegment(path, ref pos, out start, out length)) return false;

                var segment = segments[i];

                if (segment.IsParameter)
                {
                    string value;

                    // 路径已整体解码过：直接取原文，%25 这类内容不能被二次解码
                    if (alreadyDecoded)
                        value = path.Substring(start, length);
                    else if (!TryDecodeSegment(path, start, length, out value))
                        return false;

                    if (collected == null) collected = new List<KeyValuePair<string, string>>();

                    collected.Add(new KeyValuePair<string, string>(segment.Value, value));
                }
                else if (!SegmentEquals(path, start, length, segment.Value))
                {
                    return false;
                }
            }

            // 段数必须完全一致，多出来的段判为不匹配
            int restStart;
            int restLength;

            if (TryReadSegment(path, ref pos, out restStart, out restLength)) return false;

            values = collected;

            return true;
        }

        /// <summary>
        /// 解码单个路径段；解码后含 / 说明请求用 %2F 越段，判为不匹配
        /// </summary>
        /// <param name="path">归一化路径</param>
        /// <param name="start">段首索引</param>
        /// <param name="length">段长度</param>
        /// <param name="value">解码后的段值</param>
        /// <returns>可作为单个路径段使用时返回 true</returns>
        static bool TryDecodeSegment(string path, int start, int length, out string value)
        {
            var raw = path.Substring(start, length);

            if (raw.IndexOf('%') < 0)
            {
                value = raw;
            }
            else
            {
                try
                {
                    value = Uri.UnescapeDataString(raw);
                }
                catch (UriFormatException)
                {
                    // 非法转义不能让坏请求打断匹配，按原文继续比较
                    value = raw;
                }
                catch (ArgumentException)
                {
                    value = raw;
                }
            }

            return value.IndexOf(Slash) < 0;
        }

        static string CollectAllow(List<Entry> entries)
        {
            var builder = new StringBuilder();

            for (var v = 0; v < AllVerbs.Length; v++)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].DeclaresVerb(AllVerbs[v])) continue;

                    if (builder.Length > 0) builder.Append(", ");

                    builder.Append(AllVerbs[v]);

                    break;
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        #endregion

        /// <summary>
        /// 一个模板段：占位段携带参数名，静态段携带原文
        /// </summary>
        sealed class Segment
        {
            internal bool IsParameter { get; set; }

            internal string Value { get; set; }
        }

        /// <summary>
        /// 一个 action 的属性路由条目
        /// </summary>
        sealed class Entry
        {
            internal Type ControllerType { get; set; }

            internal string ActionName { get; set; }

            internal string Template { get; set; }

            /// <summary>
            /// 路径形状：参数段归一化为 {}，静态段保留原文
            /// </summary>
            internal string Shape { get; set; }

            internal Segment[] Segments { get; set; }

            internal string[] HttpMethods { get; set; }

            internal int Order { get; set; }

            internal int StaticSegmentCount { get; set; }

            internal bool HasParameters { get; set; }

            internal bool AllowsMethod(string method)
            {
                var methods = HttpMethods;

                if (methods == null) return true;

                for (var i = 0; i < methods.Length; i++)
                {
                    if (string.Compare(methods[i], method, true) == 0) return true;
                }

                return false;
            }

            internal bool DeclaresVerb(string verb)
            {
                var methods = HttpMethods;

                if (methods == null) return false;

                for (var i = 0; i < methods.Length; i++)
                {
                    if (string.Compare(methods[i], verb, true) == 0) return true;
                }

                return false;
            }
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