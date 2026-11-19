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
*命名空间：SAEA.MVC
*文件名： OutputCacheAttribute
*版本号： v26.4.23.1
*唯一标识：d895af7f-65ac-42c6-ac64-b79ef86ed2f5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/12/14 11:05:54
*描述：OutputCacheAttribute接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 22:00:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：缓存键加入规范化 query（按键 Ordinal 升序拼接），避免同路径不同 query 串号；纠正缓存键注释
*
*****************************************************************************/
using SAEA.Common.Caching;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// 缓存
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class OutputCacheAttribute : ActionFilterAttribute, IFilter
    {
        int _duration = 60;

        /// <summary>
        /// 以缓存秒数创建输出缓存过滤器，执行顺序固定为最前
        /// </summary>
        /// <param name="Duration">缓存有效秒数</param>
        public OutputCacheAttribute(int Duration = 60)
        {
            _duration = Duration;
            Order = -1;
        }


        /// <summary>
        /// 计算输出缓存键：请求动词 + 规范路径 + 规范化 query
        /// </summary>
        /// <returns>如 GET:/users/5?a=1&amp;b=2，无 query 时为 GET:/users/5</returns>
        string GetCacheKey()
        {
            var context = HttpContext.Current;

            // 属性路由命中时 RoutingPath 是本次实际命中的请求路径（不是模板），否则 /users/5 与 /users/7
            // 会得到同一个键、路径参数场景仍会串号；非 MVC 上下文时回退到 Request.Url
            var path = (context as HttpContext)?.RoutingPath;

            if (string.IsNullOrEmpty(path))
                path = context.Request.Url;

            if (string.IsNullOrEmpty(path))
                path = "/";

            // 尾斜杠不影响语义，统一去掉；根路径本身保留
            if (path.Length > 1)
                path = path.TrimEnd('/');

            if (path.Length == 0)
                path = "/";

            // 过滤器实例被 RouteTable 缓存并跨线程复用，键只做即时计算，不能存到实例字段上
            var key = context.Request.Method + ":" + path;

            var query = context.Request.Query;

            // 无 query 时键保持原有形态，避免影响既有缓存行为
            if (query == null || query.Count == 0)
                return key;

            // 键字典不区分大小写，但排序用 Ordinal 保证同一组参数无论请求顺序如何都得到相同串
            var names = new List<string>(query.Keys);

            names.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder();

            for (var i = 0; i < names.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append('&');

                // 值为 null 时按空串拼接，与 k= 的语义一致
                builder.Append(names[i]).Append('=').Append(query[names[i]] ?? string.Empty);
            }

            return key + "?" + builder.ToString();
        }

        /// <summary>
        /// 执行 action 前尝试命中缓存
        /// </summary>
        /// <returns>命中的缓存结果，未命中返回 null</returns>
        public override ActionResult OnActionExecuting()
        {
            return OutputCacheManager.Get(GetCacheKey());
        }

        /// <summary>
        /// 执行 action 后，无内容的结果补取缓存，有内容的写入缓存
        /// </summary>
        /// <param name="result">action 执行结果，可能被替换为缓存结果</param>
        public override void OnActionExecuted(ref ActionResult result)
        {
            if (result.Content == null)
                result = OutputCacheManager.Get(GetCacheKey());
            else
                OutputCacheManager.Set(GetCacheKey(), result, _duration);
        }
    }

    /// <summary>
    /// 输出缓存管理器，按请求动词、规范路径与规范化 query 缓存响应结果
    /// </summary>
    internal static class OutputCacheManager
    {
        static MemoryCache<ActionResult> _cache;

        static OutputCacheManager()
        {
            _cache = new MemoryCache<ActionResult>();
        }

        public static void Set(string key, ActionResult val, int timeout = 60)
        {
            _cache.Set(key, val, TimeSpan.FromSeconds(timeout));
        }

        public static ActionResult Get(string key)
        {
            return _cache.Get(key);
        }
    }
}