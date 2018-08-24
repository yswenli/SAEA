/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： HashMap
*版本号： V1.0.0.0
*唯一标识：910dfd6d-5160-474c-9b4d-ec13ab36f614
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 17:57:20
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 17:57:20
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 定义一个类似redis 的 hashset结构
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    public class HashMap<T1, T2, T3> : IDisposable
    {
        object _locker = new object();

        Dictionary<T1, Dictionary<T2, T3>> _hashs = new Dictionary<T1, Dictionary<T2, T3>>();

        /// <summary>
        /// 添加或更新
        /// </summary>
        /// <param name="hashID"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Set(T1 hashID, T2 key, T3 val)
        {
            lock (_locker)
            {
                if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                {
                    if (d.ContainsKey(key))
                    {
                        d[key] = val;
                    }
                    else
                    {
                        d.Add(key, val);
                    }
                }
                else
                {
                    var d2 = new Dictionary<T2, T3>
                    {
                        { key, val }
                    };
                    _hashs.Add(hashID, d2);
                }
            }
        }
        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="hashID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public T3 Get(T1 hashID, T2 key)
        {
            lock (_locker)
            {
                if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                {
                    if (d.ContainsKey(key))
                    {
                        return d[key];
                    }
                }
                return default(T3);
            }
        }
        /// <summary>
        /// 获取全部集合
        /// </summary>
        /// <param name="hashID"></param>
        /// <returns></returns>
        public Dictionary<T2, T3> GetAll(T1 hashID)
        {
            lock (_locker)
            {
                if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                {
                    return d;
                }
                return null;
            }
        }
        /// <summary>
        /// 是否存在
        /// </summary>
        /// <param name="hashID"></param>
        /// <returns></returns>
        public bool Exits(T1 hashID)
        {
            lock (_locker)
            {
                return _hashs.ContainsKey(hashID);
            }
        }
        /// <summary>
        /// 是否存在
        /// </summary>
        /// <param name="hashID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exits(T1 hashID, T2 key)
        {
            lock (_locker)
            {
                if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                {
                    return d.ContainsKey(key);
                }
                return false;
            }
        }

        /// <summary>
        /// 获取全部Hashid
        /// </summary>
        /// <returns></returns>
        public List<T1> GetHashIDs()
        {
            lock (_locker)
            {
                return _hashs.Keys.ToList();
            }
        }
        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="hashID"></param>
        public void Remove(T1 hashID)
        {
            lock (_locker)
            {
                if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                {
                    d.Clear();
                    _hashs.Remove(hashID);
                }
            }
        }
        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="hashID"></param>
        /// <param name="key"></param>
        public void Remove(T1 hashID, T2 key)
        {
            lock (_locker)
            {
                if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                {
                    if (d.ContainsKey(key))
                    {
                        d.Remove(key);

                        if (d.Count == 0)
                        {
                            _hashs.Remove(hashID);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            lock (_locker)
            {
                if (_hashs != null)
                {
                    if (_hashs.Count > 0)
                    {
                        var hashIDs = _hashs.Keys;

                        foreach (var hashID in hashIDs)
                        {
                            if (_hashs.TryGetValue(hashID, out Dictionary<T2, T3> d))
                            {
                                d.Clear();
                                _hashs.Remove(hashID);
                            }
                        }
                    }
                    _hashs.Clear();
                    _hashs = null;
                }
            }
        }
    }
}
