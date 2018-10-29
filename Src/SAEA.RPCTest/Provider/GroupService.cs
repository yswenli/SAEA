/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPCTest.Provider
*文件名： GroupService
*版本号： V3.1.1.0
*唯一标识：624119be-97e0-476d-a497-2a3cc6afb0a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/29 16:00:52
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/29 16:00:52
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Common;
using SAEA.RPCTest.Provider.Model;
using System.Collections.Generic;

namespace SAEA.RPCTest.Provider
{
    [RPCService]
    public class GroupService
    {
        public List<UserInfo> Update(List<UserInfo> users)
        {
            return users;
        }


        public GroupInfo Add(string groupName, UserInfo user)
        {
            return new GroupInfo() { GroupID = 1, Name = groupName, Created = DateTimeHelper.Now, IsTemporary = false, Creator = user };
        }



        public GroupInfo GetGroupInfo(int id)
        {
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
            };
        }
    }
}
