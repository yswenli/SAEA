/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： Binding
*版本号： v4.3.1.2
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
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// 连接与主题的映射
    /// </summary>
    class Binding : ISyncBase, IDisposable
    {
        List<BindInfo> _list = new List<BindInfo>();

        object _syncLocker = new object();

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        bool _isDisposed = false;

        int _minutes = 10;

        public Binding(int minutes = 10)
        {
            _minutes = minutes;

            ThreadHelper.PulseAction(() =>
            {
                lock (_syncLocker)
                {
                    var list = _list.Where(b => b.Expired <= DateTimeHelper.Now).ToList();
                    if (list != null)
                    {
                        list.ForEach(item =>
                        {
                            _list.Remove(item);
                        });
                        list.Clear();
                        list = null;
                    }
                }
            }, new TimeSpan(0, 0, 10), _isDisposed);
        }


        public void Set(string sessionID, string name, string topic, bool isPublisher = true)
        {

            lock (_syncLocker)
            {
                var result = _list.FirstOrDefault(b => b.Name == name && b.Topic == topic);
                if (result == null)
                {
                    _list.Add(new BindInfo()
                    {
                        SessionID = sessionID,
                        Name = name,
                        Topic = topic,
                        Flag = isPublisher,
                        Expired = DateTimeHelper.Now.AddMinutes(_minutes)
                    });
                }
                else
                {
                    result.Expired = DateTimeHelper.Now.AddMinutes(_minutes);
                }
            }
        }

        public void Del(string sessionID, string topic)
        {
            lock (_syncLocker)
            {
                var result = _list.FirstOrDefault(b => b.Name == sessionID && b.Topic == topic);
                if (result != null)
                {
                    _list.Remove(result);
                }
            }
        }

        public void Remove(string sessionID)
        {
            lock (_syncLocker)
            {
                var result = _list.Where(b => b.SessionID == sessionID).ToList();
                if (result != null)
                {
                    result.ForEach((item) =>
                    {
                        _list.Remove(item);
                    });
                    result.Clear();
                }
            }
        }

        public BindInfo GetBingInfo(QueueResult sInfo)
        {
            lock (_syncLocker)
            {
                var bi = _list.FirstOrDefault(b => b.Name == sInfo.Name && b.Topic == sInfo.Topic);

                if (bi != null)
                {
                    if (bi.Expired <= DateTimeHelper.Now)
                    {
                        Remove(bi.SessionID);
                    }
                    else
                    {
                        return bi;
                    }
                }
                return null;
            }
        }

        public BindInfo GetBingInfo(string sessionID)
        {
            lock (_syncLocker)
            {
                return _list.FirstOrDefault(b => b.SessionID == sessionID);
            }
        }

        public bool Exists(QueueResult sInfo)
        {
            lock (_syncLocker)
            {
                var data = _list.FirstOrDefault(b => b.Name == sInfo.Name && b.Topic == sInfo.Topic);

                if (data != null)
                {
                    if (data.Expired <= DateTimeHelper.Now)
                    {
                        Remove(data.SessionID);

                        return false;
                    }

                    data.Expired = DateTimeHelper.Now.AddMinutes(_minutes);

                    return true;
                }
            }
            return false;
        }


        public IEnumerable<BindInfo> GetPublisher()
        {
            lock (_syncLocker)
            {
                return _list.Where(b => b.Flag);
            }
        }

        public int GetPublisherCount()
        {
            lock (_syncLocker)
            {
                return _list.Where(b => b.Flag).Count();
            }
        }

        public IEnumerable<BindInfo> GetSubscriber()
        {
            lock (_syncLocker)
            {
                return _list.Where(b => !b.Flag);
            }
        }

        public int GetSubscriberCount()
        {
            lock (_syncLocker)
            {
                return _list.Where(b => !b.Flag).Count();
            }
        }


        public void Dispose()
        {
            _isDisposed = true;
            lock (_syncLocker)
            {
                _list.Clear();
                _list = null;
            }
        }
    }
}
