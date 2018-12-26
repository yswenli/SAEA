/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPCTest.Consumer.Model
*文件名： GroupInfo
*版本号： V3.6.0.1
*唯一标识：baa6608c-e102-4715-b09e-084dc2f5d20b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/22 15:39:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/22 15:39:23
*修改人： yswenli
*版本号： V3.6.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPCTest.Provider.Model
{
    public class GroupInfo
    {
        public int GroupID
        {
            get; set;
        }

        public string Name
        {
            get; set;
        }

        public bool IsTemporary
        {
            get; set;
        }

        public DateTime Created
        {
            get; set;
        }

        public UserInfo Creator
        {
            get; set;
        }

        public List<UserInfo> Users
        {
            get; set;
        }

        //public Dictionary<int, UserInfo> Limit
        //{
        //    get; set;
        //}

    }
}
