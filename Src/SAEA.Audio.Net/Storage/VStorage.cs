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
*命名空间：SAEA.Audio.Storage
*文件名： VStorage
*版本号： v26.4.23.1
*唯一标识：c7b5cc50-50fd-4443-9299-76a17acbd4a1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：VStorage存储类
*
*=====================================================================
*修改标记
*修改时间：2021/02/21 16:29:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：VStorage存储类
*
*****************************************************************************/
using SAEA.Audio.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Audio.Storage
{
    public class VStorage : IStorage
    {
        ConcurrentDictionary<string, HashSet<string>> _channelUserMapping;

        ConcurrentDictionary<string, string> _userChannelMapping;

        public VStorage()
        {
            _channelUserMapping = new ConcurrentDictionary<string, HashSet<string>>();

            _userChannelMapping = new ConcurrentDictionary<string, string>();
        }


        public void SetUserChannelMapping(string userID, string channelID)
        {
            _userChannelMapping.AddOrUpdate(userID, channelID, (k, v) => channelID);
        }

        public string GetOrAddUserChannelMapping(string userID)
        {
            return _userChannelMapping.GetOrAdd(userID, (u) => Guid.NewGuid().ToString("N"));
        }

        public string GetUserChannelMapping(string userID)
        {
            if(_userChannelMapping.TryGetValue(userID,out string channelID))
            {
                return channelID;
            }
            return string.Empty;
        }

        public bool ExistsUserChannelMapping(string userID)
        {
            return _userChannelMapping.TryGetValue(userID, out string _);
        }

        public string TryRemoveUserChannelMapping(string userID)
        {
            if (_userChannelMapping.TryRemove(userID, out string channelID))
            {
                return channelID;
            }
            return string.Empty;
        }


        public void SetChannelUserMapping(string channelID, params string[] userIDs)
        {
            _channelUserMapping.AddOrUpdate(channelID, new HashSet<string>(userIDs.ToArray()), (k, v) =>
            {
                foreach (var userID in userIDs)
                {
                    v.Add(userID);
                }
                return v;
            });
        }

        public void TryRemoveChannelUserMapping(string channelID, string userID)
        {
            if (_channelUserMapping.TryGetValue(channelID, out HashSet<string> hs))
            {
                if (hs != null && hs.Any())
                {
                    hs.Remove(userID);
                }
                else
                {
                    _channelUserMapping.TryRemove(channelID, out HashSet<string> _);
                }
            }
        }

        public IEnumerable<string> GetChannelUsers(string channelID)
        {
            if (_channelUserMapping.TryGetValue(channelID, out HashSet<string> hs))
            {
                if (hs != null && hs.Any())
                {
                    return hs.ToArray();
                }
            }
            return null;
        }

    }
}
