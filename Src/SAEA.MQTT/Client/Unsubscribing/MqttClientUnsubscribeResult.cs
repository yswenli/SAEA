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
*命名空间：SAEA.MQTT.Client.Unsubscribing
*文件名： MqttClientUnsubscribeResult
*版本号： v26.4.23.1
*唯一标识：6c5da903-9f01-4f75-8dc9-904f1622be1d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientUnsubscribeResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Unsubscribing
{
    /// <summary>
    /// 取消订阅操作的结果，按提交的主题过滤器顺序保存每条取消订阅的服务端应答
    /// </summary>
    public class MqttClientUnsubscribeResult
    {
        /// <summary>
        /// 各主题过滤器的取消订阅结果，顺序与提交顺序一致
        /// </summary>
        public List<MqttClientUnsubscribeResultItem> Items { get; }  =new List<MqttClientUnsubscribeResultItem>();
    }
}
