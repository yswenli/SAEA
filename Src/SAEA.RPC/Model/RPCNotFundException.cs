/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Model
*文件名： RPCNotFundException
*版本号： V3.1.0.0
*唯一标识：e3ebc010-9c4e-4e2e-98a7-dd2d51599232
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 12:46:40
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 12:46:40
*修改人： yswenli
*版本号： V3.1.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Model
{
    public class RPCNotFundException : Exception
    {
        public RPCNotFundException(string message) : base(message) { }
        public RPCNotFundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
