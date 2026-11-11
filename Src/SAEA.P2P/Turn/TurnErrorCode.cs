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
*文件名： TurnErrorCode
*版本号： v26.11.5.1
*唯一标识：9c8106a9-6d7c-43b4-b4ff-60eeec379f66
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 22:30:00
*描述：TURN 错误码与默认原因短语
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 22:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 错误码与默认原因短语
*
*****************************************************************************/
using System;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 错误码常量与默认原因短语；编码沿用 <see cref="StunErrorCode"/> 的 ERROR-CODE 格式。
    /// </summary>
    public static class TurnErrorCode
    {
        /// <summary>
        /// 请求格式错误
        /// </summary>
        public const int BadRequest = 400;

        /// <summary>
        /// 未携带有效凭据
        /// </summary>
        public const int Unauthorized = 401;

        /// <summary>
        /// 凭据无权使用该分配
        /// </summary>
        public const int Forbidden = 403;

        /// <summary>
        /// 本实现不认识或明确不支持的必选属性
        /// </summary>
        public const int UnknownAttribute = 420;

        /// <summary>
        /// 请求与既有分配不匹配
        /// </summary>
        public const int AllocationMismatch = 437;

        /// <summary>
        /// nonce 已过期
        /// </summary>
        public const int StaleNonce = 438;

        /// <summary>
        /// 不支持的地址族
        /// </summary>
        public const int AddressFamilyNotSupported = 440;

        /// <summary>
        /// 凭据错误
        /// </summary>
        public const int WrongCredentials = 441;

        /// <summary>
        /// 不支持的传输协议
        /// </summary>
        public const int UnsupportedTransportProtocol = 442;

        /// <summary>
        /// 对端地址族与中继地址族不一致
        /// </summary>
        public const int PeerAddressFamilyMismatch = 443;

        /// <summary>
        /// 同一对端的连接已存在，TURN over TCP 专用
        /// </summary>
        public const int ConnectionAlreadyExists = 446;

        /// <summary>
        /// 连接超时或失败，TURN over TCP 专用
        /// </summary>
        public const int ConnectionTimeoutOrFailure = 447;

        /// <summary>
        /// 并发分配数超过配额
        /// </summary>
        public const int AllocationQuotaReached = 486;

        /// <summary>
        /// 中继端口或容量不足
        /// </summary>
        public const int InsufficientCapacity = 508;

        /// <summary>
        /// 取错误码对应的默认原因短语；未定义时返回通用短语
        /// </summary>
        /// <param name="code">错误码</param>
        /// <returns>英文原因短语</returns>
        public static string GetReason(int code)
        {
            switch (code)
            {
                case BadRequest:
                    return "Bad Request";
                case Unauthorized:
                    return "Unauthorized";
                case Forbidden:
                    return "Forbidden";
                case UnknownAttribute:
                    return "Unknown Attribute";
                case AllocationMismatch:
                    return "Allocation Mismatch";
                case StaleNonce:
                    return "Stale Nonce";
                case AddressFamilyNotSupported:
                    return "Address Family Not Supported";
                case WrongCredentials:
                    return "Wrong Credentials";
                case UnsupportedTransportProtocol:
                    return "Unsupported Transport Protocol";
                case PeerAddressFamilyMismatch:
                    return "Peer Address Family Mismatch";
                case ConnectionAlreadyExists:
                    return "Connection Already Exists";
                case ConnectionTimeoutOrFailure:
                    return "Connection Timeout or Failure";
                case AllocationQuotaReached:
                    return "Allocation Quota Reached";
                case InsufficientCapacity:
                    return "Insufficient Capacity";
                default:
                    return "Error";
            }
        }

        /// <summary>
        /// 用默认原因短语创建 ERROR-CODE 属性
        /// </summary>
        /// <param name="code">错误码，取值 300 到 699</param>
        /// <returns>错误码属性</returns>
        public static StunAttribute Create(int code)
        {
            return StunErrorCode.Create(code, GetReason(code));
        }

        /// <summary>
        /// 用指定原因短语创建 ERROR-CODE 属性
        /// </summary>
        /// <param name="code">错误码，取值 300 到 699</param>
        /// <param name="reason">原因短语，可为 null</param>
        /// <returns>错误码属性</returns>
        public static StunAttribute Create(int code, string reason)
        {
            return StunErrorCode.Create(code, reason);
        }

        /// <summary>
        /// 从消息里读取第一个错误码属性
        /// </summary>
        /// <param name="message">应答消息</param>
        /// <param name="code">错误码</param>
        /// <param name="reason">原因短语，可能为空串</param>
        /// <returns>存在且格式正确时返回 true</returns>
        public static bool TryGet(StunMessage message, out int code, out string reason)
        {
            return StunErrorCode.TryGet(message, out code, out reason);
        }
    }
}