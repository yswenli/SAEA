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
*命名空间：SAEA.DNS.Common.Utils
*文件名： ResponseException
*版本号： v26.4.23.1
*唯一标识：4cdb5c07-26cf-4b3e-a85a-6906f15332fd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：ResponseException接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.Utils
{
    /// <summary>
    /// 响应异常，携带导致失败的 DNS 响应
    /// </summary>
    public class ResponseException : Exception
    {
        private static string Format(IResponse response)
        {
            return string.Format("Invalid response received with code {0}", response.ResponseCode);
        }

        /// <summary>
        /// 创建不带消息与响应的异常
        /// </summary>
        public ResponseException() { }
        /// <summary>
        /// 用指定消息创建异常
        /// </summary>
        /// <param name="message">异常消息</param>
        public ResponseException(string message) : base(message) { }
        /// <summary>
        /// 用指定消息与内部异常创建异常
        /// </summary>
        /// <param name="message">异常消息</param>
        /// <param name="e">内部异常</param>
        public ResponseException(string message, Exception e) : base(message, e) { }

        /// <summary>
        /// 用响应创建异常，消息由响应码生成
        /// </summary>
        /// <param name="response">导致失败的响应</param>
        public ResponseException(IResponse response) : this(response, Format(response)) { }

        /// <summary>
        /// 用响应与内部异常创建异常，消息由响应码生成
        /// </summary>
        /// <param name="response">导致失败的响应</param>
        /// <param name="e">内部异常</param>
        public ResponseException(IResponse response, Exception e)
            : base(Format(response), e)
        {
            Response = response;
        }

        /// <summary>
        /// 用响应与指定消息创建异常
        /// </summary>
        /// <param name="response">导致失败的响应</param>
        /// <param name="message">异常消息</param>
        public ResponseException(IResponse response, string message)
            : base(message)
        {
            Response = response;
        }

        /// <summary>
        /// 导致失败的响应，构造函数未提供时为 null
        /// </summary>
        public IResponse Response
        {
            get;
            private set;
        }
    }
}
