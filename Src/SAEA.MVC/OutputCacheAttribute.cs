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
*修改时间：2026/11/6 13:23:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补齐英文与乱码注释的中文说明
*
*****************************************************************************/
using SAEA.Common.Caching;
using System;

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
        /// 执行 action 前尝试命中缓存
        /// </summary>
        /// <returns>命中的缓存结果，未命中返回 null</returns>
        public override ActionResult OnActionExecuting()
        {
            return OutputCacheManager.Get(HttpContext.Current.Request.Url);            
        }

        /// <summary>
        /// 执行 action 后，无内容的结果补取缓存，有内容的写入缓存
        /// </summary>
        /// <param name="result">action 执行结果，可能被替换为缓存结果</param>
        public override void OnActionExecuted(ref ActionResult result)
        {
            if (result.Content == null)
                result = OutputCacheManager.Get(HttpContext.Current.Request.Url);
            else
                OutputCacheManager.Set(HttpContext.Current.Request.Url, result, _duration);
        }
    }

    /// <summary>
    /// 输出缓存管理器，按请求地址缓存响应结果
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