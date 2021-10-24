/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Model
*类 名 称：IStorage
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
