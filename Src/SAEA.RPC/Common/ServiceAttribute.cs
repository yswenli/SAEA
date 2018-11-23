/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Common
*文件名： RPCServiceAttribute
*版本号： V3.3.3.3
*唯一标识：6e113afe-ffa5-4669-9ec3-d5e9a8f83ff6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 10:18:10
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 10:18:10
*修改人： yswenli
*版本号： V3.3.3.3
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.RPC.Common
{
    /// <summary>
    /// 标记当前类为服务提供类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RPCServiceAttribute : Attribute
    {

    }
    /// <summary>
    /// 标记当前方法不开放
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class NoRpcAttribute : Attribute
    {

    }
}
