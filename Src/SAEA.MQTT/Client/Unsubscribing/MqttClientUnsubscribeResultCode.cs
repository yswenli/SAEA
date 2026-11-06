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
*文件名： MqttClientUnsubscribeResultCode
*版本号： v26.4.23.1
*唯一标识：db68fa81-9f1c-4ee3-b1dd-a06ac287f86c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientUnsubscribeResultCode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Client.Unsubscribing
{
    /// <summary>
    /// 取消订阅确认（UNSUBACK）中单个主题过滤器的结果码
    /// </summary>
    public enum MqttClientUnsubscribeResultCode
    {
        /// <summary>
        /// 取消订阅成功
        /// </summary>
        Success = 0,
        /// <summary>
        /// 对应的订阅原本就不存在
        /// </summary>
        NoSubscriptionExisted = 17,
        /// <summary>
        /// 未指明错误
        /// </summary>
        UnspecifiedError = 128,
        /// <summary>
        /// 请求有效但当前实现不接受
        /// </summary>
        ImplementationSpecificError = 131,
        /// <summary>
        /// 客户端未通过取消订阅授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 主题过滤器格式不合法
        /// </summary>
        TopicFilterInvalid = 143,
        /// <summary>
        /// 报文标识符已被其他在途报文占用
        /// </summary>
        PacketIdentifierInUse = 145
    }
}
