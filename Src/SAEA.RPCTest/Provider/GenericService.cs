/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：IM.RPC.Test.Providers
*文件名： GenericService
*版本号： V3.3.3.1
*唯一标识：8b5f3f50-5d04-4e5a-97ac-dbe21ca2e55d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/6/11 9:28:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/6/11 9:28:31
*修改人： yswenli
*版本号： V3.3.3.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SAEA.RPC.Common;
using SAEA.RPCTest.Provider.Model;
using SAEA.RPCTest.Providers.Model;

namespace SAEA.RPCTest.Providers
{
    [RPCService]
    public class GenericService
    {
        public ActionResult<UserInfo> Get(ActionResult<UserInfo> data)
        {
            return data;
        }


        public List<string> GetListString()
        {
            var list = new List<string>();
            for (int i = 0; i < 10000; i++)
            {
                list.Add($"{i} hello");
            }
            return list;

        }
    }
}
