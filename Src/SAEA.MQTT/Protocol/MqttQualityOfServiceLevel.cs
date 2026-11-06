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
*命名空间：SAEA.MQTT.Protocol
*文件名： MqttQualityOfServiceLevel
*版本号： v26.4.23.1
*唯一标识：bb869986-850f-4f10-87d0-3147d2286431
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttQualityOfServiceLevel接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    /// <summary>
    /// MQTT 服务质量等级
    /// </summary>
    public enum MqttQualityOfServiceLevel
    {
        /// <summary>
        /// 最多投递一次，不保证送达
        /// </summary>
        AtMostOnce = 0x00,
        /// <summary>
        /// 至少投递一次，可能重复送达
        /// </summary>
        AtLeastOnce = 0x01,
        /// <summary>
        /// 恰好投递一次，通过两阶段握手避免重复
        /// </summary>
        ExactlyOnce = 0x02
    }
}
