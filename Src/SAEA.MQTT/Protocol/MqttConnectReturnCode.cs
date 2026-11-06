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
*文件名： MqttConnectReturnCode
*版本号： v26.4.23.1
*唯一标识：a619a301-3295-4d4e-8765-0a0f77e6e0a6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnectReturnCode代码枚举
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
    /// MQTT 3.1.1 连接确认（CONNACK）返回码
    /// </summary>
    public enum MqttConnectReturnCode
    {
        /// <summary>
        /// 连接被服务端接受
        /// </summary>
        ConnectionAccepted = 0x00,
        /// <summary>
        /// 服务端不支持客户端请求的协议版本
        /// </summary>
        ConnectionRefusedUnacceptableProtocolVersion = 0x01,
        /// <summary>
        /// 客户端标识符被服务端拒绝
        /// </summary>
        ConnectionRefusedIdentifierRejected = 0x02,
        /// <summary>
        /// 服务端当前不可用
        /// </summary>
        ConnectionRefusedServerUnavailable = 0x03,
        /// <summary>
        /// 用户名或密码错误
        /// </summary>
        ConnectionRefusedBadUsernameOrPassword = 0x04,
        /// <summary>
        /// 客户端未通过授权
        /// </summary>
        ConnectionRefusedNotAuthorized = 0x05
    }
}
