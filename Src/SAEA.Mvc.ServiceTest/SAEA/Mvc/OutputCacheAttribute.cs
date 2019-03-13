using SAEA.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// 缓存
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class OutputCacheAttribute : Attribute, IFilter
    {
        int _duration = -1;

        public int Order { get; set; } = -1;

        public OutputCacheAttribute(int Duration = 60)
        {
            _duration = Duration;
        }


        /// <summary>
        /// 方法执行前
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public string OnActionExecuting(HttpContext httpContext)
        {
            var result = "0,60";
            if (!httpContext.Session.CachedPath.Contains(httpContext.Request.Url))
            {
                httpContext.Session.CachedPath.Add(httpContext.Request.Url, DateTime.Now.AddSeconds(_duration));
                result = "0" + "," + _duration;
            }
            else
            {
                var cp = httpContext.Session.CachedPath.Get(httpContext.Request.Url);
                if (cp != null)
                {
                    result = "1" + "," + (int)(cp.Expired - DateTime.Now).TotalSeconds;
                }
            }
            return result;
        }
    }
}
