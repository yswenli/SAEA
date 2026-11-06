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
*文件名： MqttPayloadFormatIndicator
*版本号： v26.4.23.1
*唯一标识：be11877d-9c98-40bc-9d14-1edceb38a788
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPayloadFormatIndicator接口
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
    /// MQTT 5.0 负载格式指示，说明应用消息负载是否为 UTF-8 字符数据
    /// </summary>
    public enum MqttPayloadFormatIndicator
    {
        /// <summary>
        /// 未指明格式，负载按不透明字节处理
        /// </summary>
        Unspecified = 0,
        /// <summary>
        /// 负载为 UTF-8 编码的字符数据
        /// </summary>
        CharacterData = 1
    }
}
