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
