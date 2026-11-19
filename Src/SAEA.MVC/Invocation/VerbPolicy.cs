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
*命名空间：SAEA.MVC.Invocation
*文件名： VerbPolicy
*版本号： v26.11.16.1
*唯一标识：91c8ab79-ba4b-4e31-93d0-758982022830
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 13:58:19
*描述：action 声明动词的判定与 405 拦截
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 19:15:49
*修改人： yswenli
*版本号： v26.11.16.1
*描述：动词特性解析结果按 action 缓存，不再每请求新建特性实例数组
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SAEA.MVC.Invocation
{
    /// <summary>
    /// action 声明动词的判定：由 HttpMethodAttribute 子类得出允许的方法集合，并生成 Allow 文本
    /// </summary>
    internal static class VerbPolicy
    {
        // 动词声明属于 action 的固定元数据：GetCustomAttributes 每次调用都会新建特性实例数组，
        // 解析一次后按 MethodInfo 复用，返回值只读
        static readonly ConcurrentDictionary<MethodInfo, string[]> _allowedMethodsCache = new ConcurrentDictionary<MethodInfo, string[]>();

        /// <summary>
        /// 读取 action 上声明的动词特性，返回允许的请求方法
        /// </summary>
        /// <param name="action">目标 action 方法</param>
        /// <returns>按 HttpMethodCatalog.All 顺序去重后的允许方法；未声明任何动词特性时返回 null，表示不限制</returns>
        internal static string[] GetAllowedMethods(MethodInfo action)
        {
            if (action == null) return null;

            if (_allowedMethodsCache.TryGetValue(action, out var cached)) return cached;

            var computed = BuildAllowedMethods(action);

            _allowedMethodsCache.TryAdd(action, computed);

            return computed;
        }

        /// <summary>
        /// 解析 action 上声明的动词特性
        /// </summary>
        /// <param name="action">action 方法</param>
        /// <returns>按 HttpMethodCatalog.All 顺序去重后的允许方法；未声明任何动词特性时返回 null</returns>
        static string[] BuildAllowedMethods(MethodInfo action)
        {
            var methodAttributes = HttpMethodCatalog.FromAttributes(action.GetCustomAttributes(true));

            if (methodAttributes.Length == 0) return null;

            var result = new List<string>();

            for (var v = 0; v < HttpMethodCatalog.All.Length; v++)
            {
                var verb = HttpMethodCatalog.All[v];

                for (var i = 0; i < methodAttributes.Length; i++)
                {
                    if (string.Compare(methodAttributes[i].Method, verb, true) != 0) continue;

                    result.Add(verb);

                    break;
                }
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        /// <summary>
        /// 生成 Allow 报头文本：按固定顺序输出，声明 GET 时同步给出隐式允许的 HEAD；
        /// OPTIONS 只在显式声明时追加到末位
        /// </summary>
        /// <param name="allowedMethods">action 声明的方法集合，可为 null</param>
        /// <returns>用逗号加空格分隔的方法名；没有任何可列出的方法时返回 null，调用方不应写该报头</returns>
        internal static string CreateAllowHeader(string[] allowedMethods)
        {
            if (allowedMethods == null || allowedMethods.Length == 0) return null;

            var builder = new StringBuilder();

            var declaresOptions = false;

            for (var v = 0; v < HttpMethodCatalog.AllowOrder.Length; v++)
            {
                var verb = HttpMethodCatalog.AllowOrder[v];

                var declared = false;

                for (var i = 0; i < allowedMethods.Length; i++)
                {
                    if (string.Compare(allowedMethods[i], verb, true) != 0) continue;

                    declared = true;

                    break;
                }

                // 声明 GET 即隐含 HEAD：HEAD 复用 GET 的执行结果，Allow 里也要同步给出
                if (!declared && HttpMethodCatalog.IsHead(verb))
                {
                    for (var i = 0; i < allowedMethods.Length; i++)
                    {
                        if (string.Compare(allowedMethods[i], ConstHelper.GET, true) != 0) continue;

                        declared = true;

                        break;
                    }
                }

                if (!declared) continue;

                if (builder.Length > 0) builder.Append(", ");

                builder.Append(verb);
            }

            for (var i = 0; i < allowedMethods.Length; i++)
            {
                if (!HttpMethodCatalog.IsOptions(allowedMethods[i])) continue;

                declaresOptions = true;

                break;
            }

            return HttpMethodCatalog.WithExplicitOptions(builder.Length == 0 ? null : builder.ToString(), declaresOptions);
        }

        /// <summary>
        /// 判断请求方法是否在允许集合内，比较忽略大小写
        /// </summary>
        /// <param name="allowedMethods">允许的请求方法集合，不能为 null</param>
        /// <param name="method">请求行中的方法</param>
        /// <returns>命中任一允许方法时返回 true；只声明 GET 时 HEAD 也视为允许</returns>
        internal static bool IsMethodAllowed(string[] allowedMethods, string method)
        {
            if (allowedMethods == null || method == null) return false;

            for (var i = 0; i < allowedMethods.Length; i++)
            {
                if (string.Compare(allowedMethods[i], method, StringComparison.OrdinalIgnoreCase) == 0) return true;
            }

            // HEAD 复用 GET：只声明 GET 的 action 也要响应 HEAD 探测
            if (HttpMethodCatalog.IsHead(method))
            {
                for (var i = 0; i < allowedMethods.Length; i++)
                {
                    if (string.Compare(allowedMethods[i], ConstHelper.GET, StringComparison.OrdinalIgnoreCase) == 0) return true;
                }
            }

            return false;
        }
    }
}