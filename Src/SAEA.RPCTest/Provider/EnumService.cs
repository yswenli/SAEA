/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPCTest.Consumer.Model
*文件名： GroupInfo
*版本号： V4.1.2.2
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
*版本号： V4.1.2.2
*描述：
*
*****************************************************************************/
using SAEA.RPC.Common;
using SAEA.RPCTest.Provider.Model;

namespace SAEA.RPCTest.Provider
{
    [RPCService]
    public class EnumService
    {
        public ReturnEnum GetEnum(EnumServiceType est)
        {
            return ReturnEnum.Biggest;
        }
    }
}
