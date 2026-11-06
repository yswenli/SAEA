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
*命名空间：SAEA.Http
*文件名： HttpSession
*版本号： v26.4.23.1
*唯一标识：ff44bb4e-1640-4ddd-b8ce-57da0b279d0d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/12/13 15:23:48
*描述：HttpSession接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;

using SAEA.Common;

namespace SAEA.Http
{
    /// <summary>
    /// Http 会话，按键存放数据，写入时刷新为 20 分钟后过期
    /// </summary>
    public class HttpSession : IDisposable
    {
        /// <summary>
        /// 关联outputcache使用
        /// </summary>
        public string CacheCalcString { get; set; } = "-1,-1";

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed { get; set; } = false;

        /// <summary>
        /// 会话标识
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 会话数据容器，键不区分大小写且线程安全
        /// </summary>
        public ConcurrentDictionary<string, object> Dictionary { get; set; }

        /// <summary>
        /// 过期时间，构建与写入时置为当前时间加 20 分钟
        /// </summary>
        public DateTime Expired { get; set; }

        /// <summary>
        /// 创建会话，初始过期时间为 20 分钟后
        /// </summary>
        /// <param name="id">会话标识</param>
        internal HttpSession(string id) : base()
        {
            ID = id;
            Dictionary = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Expired = DateTimeHelper.Now.AddMinutes(20);
        }

        /// <summary>
        /// 按 key 读写会话值，写入时刷新过期时间
        /// </summary>
        /// <param name="key">会话键</param>
        /// <returns>会话值，键不存在或会话已过期时返回 null</returns>
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
        /// 判断会话中是否存在指定键，不校验是否过期
        /// </summary>
        /// <param name="key">会话键</param>
        /// <returns>存在返回 true</returns>
        public bool ContainsKey(string key)
        {
            return Dictionary.ContainsKey(key);
        }

        /// <summary>
        /// 移除指定键的会话值
        /// </summary>
        /// <param name="key">会话键</param>
        /// <returns>移除成功返回 true，键不存在返回 false</returns>
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
        /// 清空会话中的全部键值
        /// </summary>
        public void Clear()
        {
            Dictionary.Clear();
        }

        /// <summary>
        /// 清空会话数据并标记为已释放
        /// </summary>
        public void Dispose()
        {
            Clear();
            IsDisposed = true;
        }
    }
}