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
*命名空间：SAEA.DNS.Model
*文件名： ResponseException
*版本号： v26.4.23.1
*唯一标识：4d655103-cf2d-4263-80c0-b284d73f0dd3
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

namespace SAEA.DNS.Model
{
    /// <summary>
    /// 响应异常
    /// </summary>
    public class ResponseException : Exception
    {
        private static string Format(IResponse response)
        {
            return string.Format("Invalid response received with code {0}", response.ResponseCode);
        }

        /// <summary>
        /// 初始化响应异常
        /// </summary>
        public ResponseException() { }

        /// <summary>
        /// 以错误消息初始化响应异常
        /// </summary>
        /// <param name="message">错误消息</param>
        public ResponseException(string message) : base(message) { }

        /// <summary>
        /// 以错误消息和内部异常初始化响应异常
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="e">内部异常</param>
        public ResponseException(string message, Exception e) : base(message, e) { }

        /// <summary>
        /// 以响应码自动生成的消息初始化响应异常
        /// </summary>
        /// <param name="response">出错的响应</param>
        public ResponseException(IResponse response) : this(response, Format(response)) { }

        /// <summary>
        /// 以响应和内部异常初始化响应异常
        /// </summary>
        /// <param name="response">出错的响应</param>
        /// <param name="e">内部异常</param>
        public ResponseException(IResponse response, Exception e)
            : base(Format(response), e)
        {
            Response = response;
        }

        /// <summary>
        /// 以响应和自定义消息初始化响应异常
        /// </summary>
        /// <param name="response">出错的响应</param>
        /// <param name="message">错误消息</param>
        public ResponseException(IResponse response, string message)
            : base(message)
        {
            Response = response;
        }

        /// <summary>
        /// 出错的响应
        /// </summary>
        public IResponse Response
        {
            get;
            private set;
        }
    }
}
