/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPCTest.Provider
*文件名： GroupService
*版本号： v7.0.0.1
*唯一标识：624119be-97e0-476d-a497-2a3cc6afb0a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/29 16:00:52
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/29 16:00:52
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC;
using SAEA.RPCTest.Provider.Model;
using System.Collections.Generic;

namespace SAEA.RPCTest.Provider
{
    /// <summary>
    /// 组服务示例，验证用户列表与组信息等复杂对象的远程调用
    /// </summary>
    [RPCService]
    public class GroupService
    {
        /// <summary>
        /// 回传传入的用户列表，用于验证列表序列化
        /// </summary>
        /// <param name="users">用户列表</param>
        /// <returns>与入参相同的用户列表</returns>
        public List<UserInfo> Update(List<UserInfo> users)
        {
            return users;
        }


        /// <summary>
        /// 按组名与创建者构造组信息
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <param name="user">组的创建者</param>
        /// <returns>组 ID 固定为 1、创建时间为当前时间、非临时的组信息</returns>
        public GroupInfo Add(string groupName, UserInfo user)
        {
            return new GroupInfo() { GroupID = 1, Name = groupName, Created = DateTimeHelper.Now, IsTemporary = false, Creator = user };
        }



        /// <summary>
        /// 返回固定构造的示例组信息
        /// </summary>
        /// <param name="id">组 ID，当前实现未使用</param>
        /// <returns>含 1 个成员和 1 条限制记录的组信息</returns>
        public GroupInfo GetGroupInfo(int id)
        {
            var limit = new Dictionary<int, UserInfo>();

            limit.Add(1, new UserInfo()
            {

                ID = 1,
                Birthday = DateTimeHelper.Now.AddYears(-100),
                UserName = "yswenli",
                Score = 99.021M
            });

            return new GroupInfo()
            {
                GroupID = 1,
                IsTemporary = false,
                Name = "yswenli group",
                Created = DateTimeHelper.Now,
                Creator = new UserInfo()
                {

                    ID = 1,
                    Birthday = DateTimeHelper.Now.AddYears(-100),
                    UserName = "yswenli"
                },
                Users = new System.Collections.Generic.List<UserInfo>()
                {
                    new UserInfo()
                    {

                        ID = 1,
                        Birthday = DateTimeHelper.Now.AddYears(-100),
                        UserName = "yswenli"
                    }
                }
                ,
                Limit = limit
            };
        }
    }
}
