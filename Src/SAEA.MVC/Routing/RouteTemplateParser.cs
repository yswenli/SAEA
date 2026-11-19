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
*文件名： RouteTemplateParser
*版本号： v26.11.16.1
*唯一标识：418fecf3-60a6-47b7-8c33-295cbd710f71
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:27:41
*描述：属性路由模板的解析、校验与条目构建
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:55:14
*修改人： yswenli
*版本号： v26.11.16.1
*描述：动词收集改为读取 HttpMethodAttribute 子类，支持 PUT/DELETE/PATCH/HEAD/OPTIONS 与多动词声明
*
*****************************************************************************/
using SAEA.Common;

using SAEA.MVC.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 属性路由模板的解析、校验与条目构建
    /// </summary>
    internal static class RouteTemplateParser
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

        internal static AttributeRouteEntry CreateEntry(Type controllerType, MethodInfo action, List<string> classSegments, int order)
        {
            var route = AttributeRouteTable.GetAttribute<RouteAttribute>(action);

            var verbAttributes = HttpMethodCatalog.FromAttributes(action.GetCustomAttributes(true));

            var routeTemplate = route == null ? null : route.Template;

            var verbTemplate = FindVerbTemplate(verbAttributes);

            var sources = 0;

            if (route != null) sources++;

            for (var i = 0; i < verbAttributes.Length; i++)
            {
                if (verbAttributes[i].Template != null) sources++;
            }

            var owner = Describe(controllerType, action);

            if (sources > 1)
                throw new ArgumentException($"{owner} 的路径声明不唯一：[Route]、[HttpGet(模板)] 等带模板的动词特性中同时出现了多个带模板的特性");

            var methodTemplate = routeTemplate;

            if (methodTemplate == null) methodTemplate = verbTemplate;

            var hasVerb = verbAttributes.Length > 0;

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

            // 同一 action 上的多个动词特性按声明顺序去重合并，null 表示不限定动词
            var verbs = new List<string>();

            for (var i = 0; i < verbAttributes.Length; i++)
            {
                var verb = verbAttributes[i].Method;

                var exists = false;

                for (var j = 0; j < verbs.Count; j++)
                {
                    if (string.Compare(verbs[j], verb, true) != 0) continue;

                    exists = true;

                    break;
                }

                if (!exists) verbs.Add(verb);
            }

            var entry = new AttributeRouteEntry
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
        /// 取第一个带模板的动词特性；同一 action 上出现多个带模板特性属于非法声明，构建期已先行拦截
        /// </summary>
        /// <param name="verbAttributes">action 上的动词特性</param>
        /// <returns>第一个非空模板；没有时返回 null</returns>
        private static string FindVerbTemplate(HttpMethodAttribute[] verbAttributes)
        {
            for (var i = 0; i < verbAttributes.Length; i++)
            {
                if (verbAttributes[i].Template != null) return verbAttributes[i].Template;
            }

            return null;
        }

        /// <summary>
        /// 逐 action 替换段里的 [controller] 与 [action] 令牌，类级模板中的令牌因此按每个 action 分别取值
        /// </summary>
        /// <param name="segments">拼接后的段列表，令牌段会被原地替换为实际名称</param>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="action">action 方法</param>
        /// <param name="owner">声明位置描述，仅用于异常消息</param>
        /// <param name="template">替换前的模板原文，仅用于异常消息</param>
        internal static void ReplaceTokens(List<string> segments, Type controllerType, MethodInfo action, string owner, string template)
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
        private static string TrimSuffix(string value, string suffix, string token, string template, string owner)
        {
            if (value.Length >= suffix.Length && string.Compare(value, value.Length - suffix.Length, suffix, 0, suffix.Length, StringComparison.OrdinalIgnoreCase) == 0)
                value = value.Substring(0, value.Length - suffix.Length);

            if (value.Length == 0)
                throw new ArgumentException($"路由模板 \"{template}\" 的令牌 \"{token}\" 无法取值：{owner} 去掉结尾 {suffix} 后缀后为空");

            return value;
        }

        /// <summary>
        /// 按 / 切分模板并校验每个段，连续 / 与首尾 / 直接忽略
        /// </summary>
        /// <param name="template">模板原文，可为 null 或空</param>
        /// <param name="owner">声明位置描述，仅用于异常消息</param>
        /// <returns>去空段后的段列表</returns>
        internal static List<string> SplitTemplate(string template, string owner)
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
        private static void ValidateSegment(string segment, string owner, string template)
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

        internal static string BuildTemplate(List<string> segments)
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
        internal static string BuildShape(List<string> segments)
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

        internal static AttributeRouteEntry.Segment[] BuildSegments(List<string> segments)
        {
            var result = new AttributeRouteEntry.Segment[segments.Count];

            for (var i = 0; i < segments.Count; i++)
            {
                var text = segments[i];

                var isParameter = text.Length > 2 && text[0] == OpenBrace && text[text.Length - 1] == CloseBrace;

                result[i] = new AttributeRouteEntry.Segment
                {
                    IsParameter = isParameter,
                    Value = isParameter ? text.Substring(1, text.Length - 2) : text
                };
            }

            return result;
        }

        internal static string Describe(Type controllerType, MethodInfo action)
        {
            return action == null ? controllerType.Name : controllerType.Name + "/" + action.Name;
        }
    }
}