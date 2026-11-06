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
*文件名： MqttUnsubscribeReasonCode
*版本号： v26.4.23.1
*唯一标识：4d9d8751-f2b5-4a65-9bd9-878fc3b4efa2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttUnsubscribeReasonCode接口
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
    /// MQTT 5.0 取消订阅确认（UNSUBACK）原因码
    /// </summary>
    public enum MqttUnsubscribeReasonCode
    {
        /// <summary>
        /// 取消订阅成功
        /// </summary>
        Success = 0,
        /// <summary>
        /// 服务端不存在该订阅
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
        /// 未通过授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 主题过滤器格式不合法
        /// </summary>
        TopicFilterInvalid = 143,
        /// <summary>
        /// 报文标识符已被占用
        /// </summary>
        PacketIdentifierInUse = 145
    }
}
