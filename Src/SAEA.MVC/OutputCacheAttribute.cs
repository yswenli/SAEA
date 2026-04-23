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
*唯一标识：64589a85-682e-4cd8-9127-2c6a3f5b22bb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/12/14 11:05:54
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/12/14 11:05:54
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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

        public OutputCacheAttribute(int Duration = 60)
        {
            _duration = Duration;
            Order = -1;
        }


        public override ActionResult OnActionExecuting()
        {
            return OutputCacheManager.Get(HttpContext.Current.Request.Url);            
        }

        public override void OnActionExecuted(ref ActionResult result)
        {
            if (result.Content == null)
                result = OutputCacheManager.Get(HttpContext.Current.Request.Url);
            else
                OutputCacheManager.Set(HttpContext.Current.Request.Url, result, _duration);
        }
    }

    /// <summary>
    /// OutputCacheManager
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