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
*文件名： MqttPingReqPacket
*版本号： v26.4.23.1
*唯一标识：54f41184-2cbf-47b5-9ddc-3792d4870fef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPingReqPacket接口
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
    /// PINGREQ 报文，客户端用于探活
    /// </summary>
    public sealed class MqttPingReqPacket : MqttBasePacket
    {
        /// <summary>
        /// 共享实例，报文无载荷可安全复用
        /// </summary>
        // This is a minor performance improvement.
        public static MqttPingReqPacket Instance = new MqttPingReqPacket();

        /// <summary>
        /// 返回固定文本 PingReq
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            return "PingReq";
        }
    }
}
