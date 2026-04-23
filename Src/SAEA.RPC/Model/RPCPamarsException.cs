/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Model
*文件名： RPCPamarsException
*版本号： v26.4.23.1
*唯一标识：dea1d587-3e5b-4468-b362-ccb46a23a0d1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/17 12:58:14
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 12:58:14
*修改人： yswenli
*版本号： v26.4.23.1
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