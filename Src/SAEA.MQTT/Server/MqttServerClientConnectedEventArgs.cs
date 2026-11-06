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
*文件名： MqttServerClientConnectedEventArgs
*版本号： v26.4.23.1
*唯一标识：284fb1ad-84aa-4d97-afda-c3dc98a307ab
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientConnectedEventArgs接口
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
    /// 客户端连接成功事件的数据，携带客户端标识
    /// </summary>
    public class MqttServerClientConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建连接成功事件数据
        /// </summary>
        /// <param name="clientId">已连接客户端的标识，不能为 null</param>
        public MqttServerClientConnectedEventArgs(string clientId)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        }

        /// <summary>
        /// 已连接客户端的标识
        /// </summary>
        public string ClientId { get; }
    }
}
