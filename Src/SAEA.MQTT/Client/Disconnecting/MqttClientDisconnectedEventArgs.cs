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
*命名空间：SAEA.MQTT.Client.Disconnecting
*文件名： MqttClientDisconnectedEventArgs
*版本号： v26.4.23.1
*唯一标识：71f5f545-ce8a-4dba-894c-012a87ac5680
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientDisconnectedEventArgs接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using SAEA.MQTT.Client.Connecting;

namespace SAEA.MQTT.Client.Disconnecting
{
    /// <summary>
    /// 客户端连接断开事件的参数
    /// </summary>
    public sealed class MqttClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建连接断开事件参数
        /// </summary>
        /// <param name="clientWasConnected">断开前是否已建立连接，连接尝试失败时为 false</param>
        /// <param name="exception">导致断开的原因异常，正常断开为 null</param>
        /// <param name="authenticateResult">本次连接的认证结果，未完成认证时为 null</param>
        /// <param name="reason">断开原因</param>
        public MqttClientDisconnectedEventArgs(bool clientWasConnected, Exception exception, MqttClientAuthenticateResult authenticateResult, MqttClientDisconnectReason reason)
        {
            ClientWasConnected = clientWasConnected;
            Exception = exception;
            AuthenticateResult = authenticateResult;
            Reason = reason;

            ReasonCode = reason;
        }

        /// <summary>
        /// 断开前是否已建立连接
        /// </summary>
        public bool ClientWasConnected { get; }

        /// <summary>
        /// 导致断开的异常，正常断开或对端发起断开时为 null
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 本次连接的认证结果，连接尚未完成认证时为 null
        /// </summary>
        public MqttClientAuthenticateResult AuthenticateResult { get; }

        /// <summary>
        /// 断开原因
        /// </summary>
        public MqttClientDisconnectReason Reason { get; set; }

        /// <summary>
        /// 已弃用，请改用 <see cref="Reason"/>
        /// </summary>
        [Obsolete("Please use 'Reason' instead. This property will be removed in the future!")]
        public MqttClientDisconnectReason ReasonCode { get; set; }
    }
}
