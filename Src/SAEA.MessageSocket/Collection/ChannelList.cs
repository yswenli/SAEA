/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： Class1
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Caching;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.MessageSocket.Collection
{
    class ChannelList
    {
        MemoryCache<ChannelInfo> _cache = new MemoryCache<ChannelInfo>();

        object _syncLocker = new object();

        public ChannelList()
        {
            _cache = new MemoryCache<ChannelInfo>();
        }

        public ChannelInfo Get(string name)
        {
            return _cache.Get(name);
        }

        public bool Subscribe(string name, string ID)
        {
            lock (_syncLocker)
            {
                var ci = _cache.Get(name);

                if (ci != null && ci.Members != null)
                {
                    var cm = new ChannelMemberInfo()
                    {
                        ID = ID,
                        Joined = DateTimeHelper.Now
                    };

                    if (!ci.Members.Exists(b => b.ID == ID))
                    {
                        ci.Members.Add(cm);
                    }
                }
                else
                {
                    ci = new ChannelInfo()
                    {
                        Name = name,
                        Creator = ID,
                        Created = DateTimeHelper.Now
                    };

                    var cm = new ChannelMemberInfo()
                    {
                        ID = ID,
                        Joined = DateTimeHelper.Now
                    };

                    ci.Members = new List<ChannelMemberInfo>();
                    ci.Members.Add(cm);

                    _cache.Set(name, ci, TimeSpan.FromDays(1));
                }
                return true;
            }

        }

        public bool Unsubscribe(string name, string ID)
        {
            lock (_syncLocker)
            {
                var ci = _cache.Get(name);

                if (ci != null)
                {
                    if (ci.Creator == ID)
                    {
                        ci.Members.Clear();
                        _cache.Del(name);
                    }
                    else
                    {
                        var cm = ci.Members.FirstOrDefault(b => b.ID == ID);
                        if (cm != null)
                        {
                            ci.Members.Remove(cm);
                            _cache.Set(name, ci, TimeSpan.FromDays(1));
                        }
                    }
                }

                return false;
            }
                
        }

    }

    class ChannelMemberInfo
    {
        public string ID
        {
            get; set;
        }
        public DateTime Joined
        {
            get; set;
        }
    }

    class ChannelInfo
    {
        public string Name
        {
            get; set;
        }

        public string Creator
        {
            get; set;
        }

        public DateTime Created
        {
            get; set;
        }

        public List<ChannelMemberInfo> Members
        {
            get; set;
        }
    }


}
