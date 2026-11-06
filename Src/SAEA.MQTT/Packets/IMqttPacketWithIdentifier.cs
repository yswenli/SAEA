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
*命名空间：SAEA.MQTT.Packets
*文件名： IMqttPacketWithIdentifier
*版本号： v26.4.23.1
*唯一标识：494fae0c-ac04-4029-8fc8-02b8a8939eda
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttPacketWithIdentifier接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// 带报文标识符的报文，SUBSCRIBE、PUBLISH 等报文借此把请求与应答配对
    /// </summary>
    public interface IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 报文标识符，取值 1 到 65535，0 表示未设置
        /// </summary>
        ushort PacketIdentifier { get; set; }
    }
}
