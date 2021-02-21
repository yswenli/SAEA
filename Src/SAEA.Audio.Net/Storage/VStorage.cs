/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Storage
*类 名 称：VStorage
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
