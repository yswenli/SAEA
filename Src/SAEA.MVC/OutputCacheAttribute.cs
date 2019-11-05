using System;

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
        /// <returns></returns>
        public string OnActionExecuting()
        {
            var result = "0,60";
            if (!HttpContext.Current.Session.CachedPath.Contains(HttpContext.Current.Request.Url))
            {
                HttpContext.Current.Session.CachedPath.Add(HttpContext.Current.Request.Url, DateTime.Now.AddSeconds(_duration));
                result = "0" + "," + _duration;
            }
            else
            {
                var cp = HttpContext.Current.Session.CachedPath.Get(HttpContext.Current.Request.Url);
                if (cp != null)
                {
                    result = "1" + "," + (int)(cp.Expired - DateTime.Now).TotalSeconds;
                }
            }
            return result;
        }
    }
}
