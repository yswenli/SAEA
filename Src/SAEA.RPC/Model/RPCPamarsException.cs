/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Model
*文件名： RPCPamarsException
*版本号： v5.0.0.1
*唯一标识：dea1d587-3e5b-4468-b362-ccb46a23a0d1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 12:58:14
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 12:58:14
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Model
{
    public class RPCPamarsException : Exception
    {
        public RPCPamarsException(string message) : base(message) { }

        public RPCPamarsException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
