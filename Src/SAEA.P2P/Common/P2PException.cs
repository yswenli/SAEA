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
*命名空间：SAEA.P2P.Common
*文件名： P2PException
*版本号： v26.4.23.1
*唯一标识：c0bb2956-9c94-4514-867d-b6d9eb658ef7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：P2PException接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Common
{
    /// <summary>
    /// P2P 业务异常，携带固定错误码便于对端与日志检索
    /// </summary>
    public class P2PException : Exception
    {
        /// <summary>
        /// 错误码，取值来自 ErrorCode 常量
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 以错误码构造异常，异常消息取该错误码的描述文本
        /// </summary>
        /// <param name="errorCode">错误码</param>
        public P2PException(string errorCode) : base(Common.ErrorCode.GetDescription(errorCode))
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 以错误码和自定义消息构造异常
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">异常消息</param>
        public P2PException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 以错误码、自定义消息和内部异常构造异常
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">异常消息</param>
        /// <param name="innerException">引发本异常的内部异常</param>
        public P2PException(string errorCode, string message, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}