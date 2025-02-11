/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： Binding
*版本号： v7.0.0.1
*唯一标识：7472dabd-1b6a-4ffe-b19f-2d1cf7348766
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 17:10:19
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 17:10:19
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// 连接与主题的映射
    /// </summary>
    class Binding : IDisposable
    {
        ConcurrentDictionary<string, BindInfo> _cahce;

        ConcurrentDictionary<string, string> _mapping;

        /// <summary>
        /// 连接与主题的映射
        /// </summary>
        public Binding()
        {
            _cahce = new ConcurrentDictionary<string, BindInfo>();

            _mapping = new ConcurrentDictionary<string, string>();
        }

        public void Set(string sessionID, string name, string topic, bool isPublisher = true)
        {
            _cahce.AddOrUpdate(sessionID + topic, (v) => new BindInfo()
            {
                SessionID = sessionID,
                Name = name,
                Topic = topic,
                Flag = isPublisher
            }, (k, v) => new BindInfo()
            {
                SessionID = sessionID,
                Name = name,
                Topic = topic,
                Flag = isPublisher
            });

            _mapping.AddOrUpdate(name, sessionID, (k, v) => sessionID);
        }

        public void Del(string sessionID, string topic)
        {
            if (_cahce.TryRemove(sessionID + topic, out BindInfo bindInfo))
            {
                _mapping.TryRemove(bindInfo.Name, out string _);
            }
        }

        public void Remove(string sessionID)
        {
            foreach (var item in _cahce.Keys)
            {
                if (item.IndexOf(sessionID) > -1)
                {
                    if (_cahce.TryRemove(item, out BindInfo bindInfo))
                    {
                        _mapping.TryRemove(bindInfo.Name, out string _);
                    }
                }
            }
        }

        public BindInfo GetBingInfo(QueueMsg sInfo)
        {
            if (_mapping.TryGetValue(sInfo.Name, out string sessionID))
            {
                if (_cahce.TryGetValue(sessionID + sInfo.Topic, out BindInfo bindInfo))
                {
                    return bindInfo;
                }
            }
            return null;
        }

        public BindInfo GetBingInfo(string sessionID)
        {
            foreach (var item in _cahce.Keys)
            {
                if (item.IndexOf(sessionID) > -1)
                {
                    return _cahce[item];
                }
            }
            return null;
        }

        public bool Exists(QueueMsg sInfo)
        {
            if (_mapping.TryGetValue(sInfo.Name, out string sessionID))
            {
                if (_cahce.TryGetValue(sessionID + sInfo.Topic, out BindInfo _))
                {
                    return true;
                }
            }
            return false;
        }


        public IEnumerable<BindInfo> GetPublisher()
        {
            return _cahce.Values.Where(b => b.Flag);            
        }

        public int GetPublisherCount()
        {
            return GetPublisher().Count();
        }

        public IEnumerable<BindInfo> GetSubscriber()
        {
            return _cahce.Values.Where(b => !b.Flag);
        }

        public int GetSubscriberCount()
        {
            return GetSubscriber().Count();
        }


        public void Dispose()
        {
            _cahce.Clear();
            _cahce = null;

        }
    }
}
