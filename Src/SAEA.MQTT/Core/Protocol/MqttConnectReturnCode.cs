/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Protocol
*类 名 称：MqttConnectReturnCode
*版 本 号： v4.1.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:37:36
*描述：
*=====================================================================
*修改时间：2019/1/15 10:37:36
*修 改 人： yswenli
*版 本 号： v4.1.2.5
*描    述：
*****************************************************************************/
namespace SAEA.MQTT.Core.Protocol
{
    public enum MqttConnectReturnCode
    {
        ConnectionAccepted = 0x00,
        ConnectionRefusedUnacceptableProtocolVersion = 0x01,
        ConnectionRefusedIdentifierRejected = 0x02,
        ConnectionRefusedServerUnavailable = 0x03,
        ConnectionRefusedBadUsernameOrPassword = 0x04,
        ConnectionRefusedNotAuthorized = 0x05
    }
}
