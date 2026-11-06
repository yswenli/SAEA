/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：IM.RPC.Test.Providers
*文件名： GenericService
*版本号： v7.0.0.1
*唯一标识：8b5f3f50-5d04-4e5a-97ac-dbe21ca2e55d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/6/11 9:28:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/6/11 9:28:31
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.RPC;
using SAEA.RPCTest.Provider.Model;
using SAEA.RPCTest.Providers.Model;
using System.Collections.Generic;

namespace SAEA.RPCTest.Providers
{
    /// <summary>
    /// 泛型服务示例，验证泛型包装与列表的远程调用
    /// </summary>
    [RPCService]
    public class GenericService
    {
        /// <summary>
        /// 回传传入的结果包装，用于验证泛型对象的序列化
        /// </summary>
        /// <param name="data">待回传的结果包装</param>
        /// <returns>与入参相同的结果包装</returns>
        public ActionResult<UserInfo> Get(ActionResult<UserInfo> data)
        {
            return data;
        }


        /// <summary>
        /// 生成固定规模的字符串列表，用于验证大集合的传输
        /// </summary>
        /// <returns>含 10000 条「序号 hello」的字符串列表</returns>
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
