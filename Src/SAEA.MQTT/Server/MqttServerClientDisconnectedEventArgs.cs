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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerClientDisconnectedEventArgs
*版本号： v26.4.23.1
*唯一标识：2d0c1d12-2c4a-4e14-9dd1-c488a5b32160
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientDisconnectedEventArgs接口
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

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 客户端断开连接事件的数据，携带客户端标识与断开方式
    /// </summary>
    public class MqttServerClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建断开连接事件数据
        /// </summary>
        /// <param name="clientId">断开连接的客户端标识，不能为 null</param>
        /// <param name="disconnectType">断开方式，用于区分客户端主动断开、服务端断开与异常断开</param>
        public MqttServerClientDisconnectedEventArgs(string clientId, MqttClientDisconnectType disconnectType)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            DisconnectType = disconnectType;
        }
        
        /// <summary>
        /// 断开连接的客户端标识
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 断开方式，用于区分客户端主动断开、服务端断开与异常断开
        /// </summary>
        public MqttClientDisconnectType DisconnectType { get; }
    }
}
