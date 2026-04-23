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
*命名空间：SAEA.MessageSocket.Collection
*文件名： GroupList
*版本号： v26.4.23.1
*唯一标识：d3005d2f-ccde-4244-bc5c-0c26561cb4f5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：GroupList接口
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：GroupList接口
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.MessageSocket.Collection
{

    class GroupList
    {
        public object SyncLocker = new object();

        public List<GroupInfo> _list = new List<GroupInfo>();

        public bool Create(string name, string ID)
        {
            lock (SyncLocker)
            {
                if (_list != null && _list.Count >= 0)
                {
                    var gi = _list.FirstOrDefault(b => b.Name == name);

                    if (gi == null)
                    {
                        gi = new GroupInfo()
                        {
                            Name = name,
                            Creator = ID,
                            Created = DateTimeHelper.Now
                        };

                        var gm = new GroupMemberInfo()
                        {
                            ID = ID,
                            Joined = DateTimeHelper.Now
                        };

                        gi.Members = new List<GroupMemberInfo>
                        {
                            gm
                        };
                        _list.Add(gi);
                        return true;
                    }
                }
            }
            return false;
        }

        public GroupInfo Get(string name)
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

        public bool Enter(string name, string ID)
        {
            lock (SyncLocker)
            {
                if (_list != null && _list.Count >= 0)
                {
                    var ci = _list.FirstOrDefault(b => b.Name == name);

                    if (ci != null && ci.Members != null)
                    {
                        var cm = new GroupMemberInfo()
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
                }
                return false;
            }
        }

        public bool Leave(string name, string ID)
        {
            lock (SyncLocker)
            {
                if (_list != null && _list.Count >= 0)
                {
                    var gi = _list.FirstOrDefault(b => b.Name == name);

                    if (gi != null && gi.Members != null)
                    {
                        if (gi.Creator == ID)
                        {
                            gi.Members.Clear();
                            _list.Remove(gi);
                        }
                        else
                        {
                            var gm = gi.Members.FirstOrDefault(b => b.ID == ID);
                            if (gm != null)
                            {
                                gi.Members.Remove(gm);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        public bool Remove(string name)
        {
            lock (SyncLocker)
            {
                if (_list != null && _list.Count >= 0)
                {
                    var gi = _list.FirstOrDefault(b => b.Name == name);

                    if (gi != null && gi.Members != null)
                    {
                        gi.Members.Clear();
                        _list.Remove(gi);
                        return true;
                    }
                }
                return false;
            }
        }
    }

    class GroupMemberInfo
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

    class GroupInfo
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

        public List<GroupMemberInfo> Members
        {
            get; set;
        }
    }


}