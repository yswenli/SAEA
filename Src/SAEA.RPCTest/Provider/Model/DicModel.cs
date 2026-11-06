/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPCTest.Consumer.Model
*文件名： GroupInfo
*版本号： v7.0.0.1
*唯一标识：baa6608c-e102-4715-b09e-084dc2f5d20b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/22 15:39:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/22 15:39:23
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.RPCTest.Provider.Model;
using System.Collections.Generic;

namespace SAEA.RPCTest.Providers.Model
{
    /// <summary>
    /// 字典传输模型，用于验证字典键值对在 RPC 中的序列化
    /// </summary>
    public class DicModel
    {
        /// <summary>
        /// 整数到字符串的字典数据
        /// </summary>
        public Dictionary<int, string> MyDate1 { get; set; }


        /// <summary>
        /// 整数到用户信息的字典数据
        /// </summary>
        public Dictionary<int, UserInfo> MyDate2 { get; set; }
    }
}
