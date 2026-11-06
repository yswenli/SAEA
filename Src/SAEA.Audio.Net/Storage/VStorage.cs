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
*修改时间：2026/11/6 12:52:33
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Audio.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Audio.Storage
{
    /// <summary>
    /// 内存版存储实现，用两张并发字典维护用户与频道的映射
    /// </summary>
    public class VStorage : IStorage
    {
        ConcurrentDictionary<string, HashSet<string>> _channelUserMapping;

        ConcurrentDictionary<string, string> _userChannelMapping;

        /// <summary>
        /// 初始化空的频道与用户映射表
        /// </summary>
        public VStorage()
        {
            _channelUserMapping = new ConcurrentDictionary<string, HashSet<string>>();

            _userChannelMapping = new ConcurrentDictionary<string, string>();
        }


        /// <summary>
        /// 设置用户所属的频道，已有映射则覆盖
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <param name="channelID">目标频道标识</param>
        public void SetUserChannelMapping(string userID, string channelID)
        {
            _userChannelMapping.AddOrUpdate(userID, channelID, (k, v) => channelID);
        }

        /// <summary>
        /// 获取用户所属的频道，没有映射时新建 32 位无连字符 GUID 作为频道标识
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>该用户的频道标识，新建时不会登记到频道用户表中</returns>
        public string GetOrAddUserChannelMapping(string userID)
        {
            return _userChannelMapping.GetOrAdd(userID, (u) => Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// 获取用户所属的频道
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>频道标识，无映射时返回空字符串</returns>
        public string GetUserChannelMapping(string userID)
        {
            if(_userChannelMapping.TryGetValue(userID,out string channelID))
            {
                return channelID;
            }
            return string.Empty;
        }

        /// <summary>
        /// 判断用户是否已有频道映射
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>存在映射返回 true，否则返回 false</returns>
        public bool ExistsUserChannelMapping(string userID)
        {
            return _userChannelMapping.TryGetValue(userID, out string _);
        }

        /// <summary>
        /// 移除用户的频道映射
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>移除前的频道标识，无映射时返回空字符串</returns>
        public string TryRemoveUserChannelMapping(string userID)
        {
            if (_userChannelMapping.TryRemove(userID, out string channelID))
            {
                return channelID;
            }
            return string.Empty;
        }


        /// <summary>
        /// 把用户加入频道，频道不存在则新建用户集合，已有用户不重复添加
        /// </summary>
        /// <param name="channelID">频道标识</param>
        /// <param name="userIDs">要加入频道的用户标识</param>
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

        /// <summary>
        /// 从频道中移除指定用户，仅当用户集合已为空时才删除整个频道条目
        /// </summary>
        /// <param name="channelID">频道标识</param>
        /// <param name="userID">要移除的用户标识</param>
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

        /// <summary>
        /// 获取频道内的用户快照
        /// </summary>
        /// <param name="channelID">频道标识</param>
        /// <returns>用户标识数组，频道不存在或集合为空时返回 null</returns>
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
