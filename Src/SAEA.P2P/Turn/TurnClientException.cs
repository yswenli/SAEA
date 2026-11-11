/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Turn
*文件名： TurnClientException
*版本号： v26.11.5.1
*唯一标识：65ac5fa3-72b8-4ee3-8ac3-d53580c1ca2b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 03:10:00
*描述：TURN 客户端统一异常
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 03:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 客户端统一异常
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 客户端统一异常：服务端错误码原样放入 <see cref="ErrorCode"/>，
    /// 传输失败、超时、取消等本机原因统一用 0，上层不需要接触 <see cref="System.Net.Sockets.SocketException"/>。
    /// 异常消息只包含错误码与原因短语，不含口令、共享密钥或 nonce。
    /// </summary>
    public sealed class TurnClientException : Exception
    {
        /// <summary>
        /// 创建异常
        /// </summary>
        /// <param name="errorCode">TURN 错误码；非服务端错误时为 0</param>
        /// <param name="reason">中文原因说明</param>
        public TurnClientException(int errorCode, string reason)
            : base(BuildMessage(errorCode, reason))
        {
            ErrorCode = errorCode;
            Reason = reason;
        }

        /// <summary>
        /// 创建异常并保留内部原因
        /// </summary>
        /// <param name="errorCode">TURN 错误码；非服务端错误时为 0</param>
        /// <param name="reason">中文原因说明</param>
        /// <param name="innerException">内部异常</param>
        public TurnClientException(int errorCode, string reason, Exception innerException)
            : base(BuildMessage(errorCode, reason), innerException)
        {
            ErrorCode = errorCode;
            Reason = reason;
        }

        /// <summary>
        /// TURN 错误码；传输失败、超时与取消等本机原因用 0
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// 原因说明
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 拼接异常消息；错误码为 0 时不显示数字，避免把本机原因误读成协议错误
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="reason">原因说明</param>
        /// <returns>异常消息</returns>
        private static string BuildMessage(int errorCode, string reason)
        {
            var text = string.IsNullOrEmpty(reason) ? "TURN 客户端操作失败" : reason;
            return errorCode == 0 ? text : text + "（TURN 错误码 " + errorCode + "）";
        }
    }
}