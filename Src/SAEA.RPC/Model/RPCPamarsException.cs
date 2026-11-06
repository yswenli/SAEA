/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Model
*文件名： RPCPamarsException
*版本号： v26.4.23.1
*唯一标识：393a738e-45b6-445d-a029-35a680e1b57d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：RPCPamarsException接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:32
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Model
{
    /// <summary>
    /// RPC 调用参数不匹配时抛出
    /// </summary>
    public class RPCPamarsException : Exception
    {
        /// <summary>
        /// 以错误消息创建异常
        /// </summary>
        /// <param name="message">错误消息</param>
        public RPCPamarsException(string message) : base(message) { }

        /// <summary>
        /// 以错误消息和内部异常创建异常
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public RPCPamarsException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}