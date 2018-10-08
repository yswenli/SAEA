/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： Class1
*版本号： V2.1.5.0
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
*版本号： V2.1.5.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SAEA.MessageSocket.Collection
{
    class ChannelList
    {
        public object SyncLocker = new object();

        public List<ChannelInfo> _list = new List<ChannelInfo>();


        public ChannelList()
        {
            //超过一天自动结束
            new Thread(new ThreadStart(() =>
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(10 * 1000);
                        lock (SyncLocker)
                        {
                            for (int i = 0; i < _list.Count; i++)
                            {
                                if (_list[i].Created.AddDays(1) < DateTimeHelper.Now)
                                {
                                    _list.Remove(_list[i]);
                                }
                            }
                        }
                    }
                }
                catch { }

            }))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            }.Start();
        }

        public ChannelInfo Get(string name)
        {
            lock (SyncLocker)
            {
                if (_list != null && _list.Count >= 0)
                {
                    return _list.FirstOrDefault(b => b.Name == name);
                }
                return null;
            }
        }

        public bool Subscribe(string name, string ID)
        {
            lock (SyncLocker)
            {
                if (_list != null && _list.Count >= 0)
                {
                    var ci = _list.FirstOrDefault(b => b.Name == name);

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

                            return true;
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

                        _list.Add(ci);

                        return true;
                    }
                }
                return false;
            }
        }

        public bool Unsubscribe(string name, string ID)
        {
            if (_list != null && _list.Count >= 0)
            {
                var ci = _list.FirstOrDefault(b => b.Name == name);

                if (ci != null && ci.Members != null)
                {
                    if (ci.Creator == ID)
                    {
                        ci.Members.Clear();
                        _list.Remove(ci);
                        return true;
                    }
                    else
                    {
                        var cm = ci.Members.FirstOrDefault(b => b.ID == ID);
                        if (cm != null)
                        {
                            ci.Members.Remove(cm);
                            return true;
                        }
                    }
                }
            }
            return false;
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
