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
*命名空间：SAEA.Audio.Model
*文件名： IStorage
*版本号： v26.4.23.1
*唯一标识：9498a3ff-60d6-4b2e-b79f-55b2cba47d1b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：IStorage接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:33
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.Audio.Model
{
    /// <summary>
    /// 频道与用户的映射存储，保存用户所在频道与频道内的用户集合
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// 获取频道内的用户标识集合
        /// </summary>
        /// <param name="channelID">频道标识</param>
        /// <returns>用户标识集合，频道不存在或没有用户时为 null</returns>
        IEnumerable<string> GetChannelUsers(string channelID);
        /// <summary>
        /// 获取用户所在频道，没有映射时为该用户新建一个频道并返回其标识
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>用户所在频道标识</returns>
        string GetOrAddUserChannelMapping(string userID);
        /// <summary>
        /// 把若干用户加入频道，已有用户不重复添加
        /// </summary>
        /// <param name="channelID">频道标识</param>
        /// <param name="userIDs">要加入频道的用户标识</param>
        void SetChannelUserMapping(string channelID, params string[] userIDs);
        /// <summary>
        /// 设置或覆盖用户所属的频道
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <param name="channelID">目标频道标识</param>
        void SetUserChannelMapping(string userID, string channelID);
        /// <summary>
        /// 获取用户所属的频道
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>频道标识，无映射时返回空字符串</returns>
        string GetUserChannelMapping(string userID);
        /// <summary>
        /// 判断用户是否已有频道映射
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>存在映射返回 true，否则返回 false</returns>
        bool ExistsUserChannelMapping(string userID);
        /// <summary>
        /// 从频道中移除指定用户
        /// </summary>
        /// <param name="channelID">频道标识</param>
        /// <param name="userID">要移除的用户标识</param>
        void TryRemoveChannelUserMapping(string channelID, string userID);
        /// <summary>
        /// 移除用户的频道映射
        /// </summary>
        /// <param name="userID">用户标识</param>
        /// <returns>移除前的频道标识，无映射时返回空字符串</returns>
        string TryRemoveUserChannelMapping(string userID);
    }
}
