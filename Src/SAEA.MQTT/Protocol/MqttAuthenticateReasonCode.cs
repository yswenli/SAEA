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
*文件名： MqttAuthenticateReasonCode
*版本号： v26.4.23.1
*唯一标识：35707942-8653-404e-849e-b2e74784e31c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttAuthenticateReasonCode接口
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
    /// MQTT 5.0 增强认证（AUTH）原因码
    /// </summary>
    public enum MqttAuthenticateReasonCode
    {
        /// <summary>
        /// 认证成功
        /// </summary>
        Success = 0,
        /// <summary>
        /// 认证尚未完成，需要继续交换认证报文
        /// </summary>
        ContinueAuthentication = 24,
        /// <summary>
        /// 客户端请求重新进行增强认证
        /// </summary>
        ReAuthenticate = 25
    }
}
