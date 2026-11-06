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
*命名空间：SAEA.MQTT.Client.Connecting
*文件名： MqttClientConnectedEventArgs
*版本号： v26.4.23.1
*唯一标识：74a568e3-a0a5-4645-9c2e-534810a97955
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientConnectedEventArgs接口
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

namespace SAEA.MQTT.Client.Connecting
{
    /// <summary>
    /// 客户端连接成功事件的参数
    /// </summary>
    public class MqttClientConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建连接成功事件参数
        /// </summary>
        /// <param name="authenticateResult">连接认证结果，不能为 null</param>
        public MqttClientConnectedEventArgs(MqttClientAuthenticateResult authenticateResult)
        {
            AuthenticateResult = authenticateResult ?? throw new ArgumentNullException(nameof(authenticateResult));
        }

        /// <summary>
        /// 服务端返回的连接认证结果
        /// </summary>
        public MqttClientAuthenticateResult AuthenticateResult { get; }
    }
}
