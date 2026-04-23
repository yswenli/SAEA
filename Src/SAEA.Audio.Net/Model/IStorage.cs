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
*唯一标识：52709edb-94e2-4614-9bf9-6ec8c364327a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/02/21 16:29:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.Audio.Model
{
    public interface IStorage
    {
        IEnumerable<string> GetChannelUsers(string channelID);
        string GetOrAddUserChannelMapping(string userID);
        void SetChannelUserMapping(string channelID, params string[] userIDs);
        void SetUserChannelMapping(string userID, string channelID);
        string GetUserChannelMapping(string userID);
        bool ExistsUserChannelMapping(string userID);
        void TryRemoveChannelUserMapping(string channelID, string userID);
        string TryRemoveUserChannelMapping(string userID);
    }
}
