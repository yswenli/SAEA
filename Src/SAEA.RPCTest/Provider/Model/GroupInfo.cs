/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPCTest.Consumer.Model
*文件名： GroupInfo
*版本号： v7.0.0.1
*唯一标识：9e0ae7f6-2b3c-4b89-b590-095db5f2766c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/22 15:39:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 21:00:00
*修改人： yswenli
*版本号： net10.0
*描述：唯一标识改为本文件专属 GUID
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.RPCTest.Provider.Model
{
    /// <summary>
    /// 组信息模型，包含创建者、成员列表与限制名单
    /// </summary>
    [Serializable]
    public class GroupInfo
    {
        /// <summary>
        /// 组唯一标识
        /// </summary>
        public int GroupID
        {
            get; set;
        }

        /// <summary>
        /// 组名称
        /// </summary>
        public string Name
        {
            get; set;
        }

        /// <summary>
        /// 是否为临时组
        /// </summary>
        public bool IsTemporary
        {
            get; set;
        }

        /// <summary>
        /// 组的创建时间
        /// </summary>
        public DateTime Created
        {
            get; set;
        }

        /// <summary>
        /// 组的创建者
        /// </summary>
        public UserInfo Creator
        {
            get; set;
        }

        /// <summary>
        /// 组的成员列表
        /// </summary>
        public List<UserInfo> Users
        {
            get; set;
        }

        /// <summary>
        /// 按用户 ID 索引的限制名单
        /// </summary>
        public Dictionary<int, UserInfo> Limit
        {
            get; set;
        }

    }
}
