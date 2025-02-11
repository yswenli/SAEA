/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpSession
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
******************************************************************************/

using System;
using System.Collections.Concurrent;

using SAEA.Common;

namespace SAEA.Http
{
    /// <summary>
    /// HttpSession
    /// </summary>
    public class HttpSession : IDisposable
    {
        /// <summary>
        /// 关联outputcache使用
        /// </summary>
        public string CacheCalcString { get; set; } = "-1,-1";

        public bool IsDisposed { get; set; } = false;

        public string ID { get; set; }

        public ConcurrentDictionary<string, object> Dictionary { get; set; }

        public DateTime Expired { get; set; }

        /// <summary>
        /// HttpSession
        /// </summary>
        /// <param name="id"></param>
        internal HttpSession(string id) : base()
        {
            ID = id;
            Dictionary = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Expired = DateTimeHelper.Now.AddMinutes(20);
        }

        /// <summary>
        /// HttpSession
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object this[string key]
        {
            get
            {
                if (Dictionary.TryGetValue(key, out object data))
                {
                    if (data != null && data != null)
                    {
                        if (Expired < DateTimeHelper.Now)
                        {
                            Dictionary.TryRemove(key, out object _);
                            return null;
                        }
                        return data;
                    }
                }
                return null;
            }
            set
            {
                Dictionary[key] = value;
                Expired = DateTimeHelper.Now.AddMinutes(20);
            }
        }

        /// <summary>
        /// ContainsKey
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(string key)
        {
            return Dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return Dictionary.TryRemove(key, out object _);
        }

        /// <summary>
        /// 重新更新计时器
        /// </summary>
        internal void Refresh()
        {
            this.Expired = DateTimeHelper.Now.AddMinutes(20);
        }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            Dictionary.Clear();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Clear();
            IsDisposed = true;
        }
    }
}
