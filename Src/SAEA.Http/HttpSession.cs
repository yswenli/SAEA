/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpSession
*版本号： v5.0.0.1
*唯一标识：2e43075f-a43d-4b60-bee1-1f9107e2d133
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/12/12 20:46:40
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/12/12 20:46:40
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
******************************************************************************/

using SAEA.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SAEA.Http
{
    /// <summary>
    /// HttpSession
    /// </summary>
    public class HttpSession : HttpSession<object>
    {
        Timer timer;

        public string ID
        {
            get;
            private set;
        }

        public DateTime Expired
        {
            get; set;
        }

        public KeyCache CachedPath
        {
            get; set;
        } = new KeyCache();

        /// <summary>
        /// 关联outputcache使用
        /// </summary>
        public string CacheCalcString { get; set; } = "-1,-1";

        internal event Action<HttpSession> OnExpired;

        internal HttpSession(string id) : base()
        {
            this.ID = id;
            this.Expired = DateTime.Now.AddMinutes(20);
            timer = new Timer(new TimerCallback((o) =>
            {
                OnExpired?.Invoke((HttpSession)o);
            }), this, (long)(new TimeSpan(0, 20, 0).TotalMilliseconds), -1);
        }

        internal void Refresh()
        {
            this.Expired = DateTime.Now.AddMinutes(20);
            timer.Change((long)(new TimeSpan(0, 20, 0).TotalMilliseconds), -1);
        }
    }


    public class HttpSession<T>
    {
        Dictionary<string, HttpSessionItem<T>> keyValuePairs;

        public HttpSession()
        {
            keyValuePairs = new Dictionary<string, HttpSessionItem<T>>();
        }

        public T this[string key]
        {
            get
            {
                var result = keyValuePairs[key];
                if (result != null) return result.Value;
                return default(T);
            }
            set
            {
                keyValuePairs[key] = new HttpSessionItem<T>(key, value);
            }
        }

        public List<string> Keys
        {
            get
            {
                return keyValuePairs.Keys.ToList();
            }
        }

        public void Remove(string key)
        {
            if (keyValuePairs.ContainsKey(key))
            {
                keyValuePairs.Remove(key);
            }
        }

        public void Clear()
        {
            keyValuePairs.Clear();
        }
    }

    /// <summary>
    /// SessionItem
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class HttpSessionItem<T>
    {
        public string Key
        {
            get; set;
        }

        public T Value
        {
            get; set;
        }

        public DateTime Expires
        {
            get; set;
        }

        public HttpSessionItem(string key, T value, DateTime expires)
        {
            this.Key = key;
            this.Value = value;
            this.Expires = expires;
        }

        public HttpSessionItem(string key, T value) : this(key, value, DateTime.Now.AddMinutes(20))
        {

        }

    }

    /// <summary>
    /// SessionManager
    /// </summary>
    internal static class HttpSessionManager
    {
        static ConcurrentDictionary<string, HttpSession> _keyValuePairs = new ConcurrentDictionary<string, HttpSession>();

        static Random random = new Random();

        /// <summary>
        /// 生成sessionID
        /// </summary>
        /// <returns></returns>
        public static string GeneratID()
        {
            var bytes = new byte[15];

            random.NextBytes(bytes);

            return StringHelper.Substring(string.Join("", bytes), 0, 15);
        }

        public static HttpSession SetAndGet(string id)
        {
            var session = _keyValuePairs.GetOrAdd(id, (k) =>
            {
                var httpSession = new HttpSession(k);
                httpSession.OnExpired += HttpSession_OnExpired;
                return httpSession;
            });
            session.Refresh();
            return session;
        }

        private static void HttpSession_OnExpired(HttpSession obj)
        {
            Remove(obj.ID);
        }

        public static HttpSession Get(string id)
        {
            var httpSession = _keyValuePairs[id];
            if (httpSession.Expired <= DateTime.Now)
            {
                _keyValuePairs.TryRemove(id, out HttpSession s);
                return null;
            }
            httpSession.Refresh();
            return httpSession;
        }

        public static void Remove(string id)
        {
            if (_keyValuePairs.TryRemove(id, out HttpSession httpSession))
            {
                httpSession.CachedPath.Clear();
                httpSession.Clear();
            }
        }

    }
}
