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
*命名空间：SAEA.MQTT
*文件名： IApplicationMessageReceiver
*版本号： v26.4.23.1
*唯一标识：ef7a6e16-4e0d-434b-bf01-24247d95994c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IApplicationMessageReceiver接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Receiving;

namespace SAEA.MQTT
{
    /// <summary>
    /// 应用消息接收者，收到的消息交由注入的处理器统一处理
    /// </summary>
    public interface IApplicationMessageReceiver
    {
        /// <summary>
        /// 收到应用消息时调用的处理器，为null时不处理收到的消息
        /// </summary>
        IMqttApplicationMessageReceivedHandler ApplicationMessageReceivedHandler { get; set; }
    }
}
