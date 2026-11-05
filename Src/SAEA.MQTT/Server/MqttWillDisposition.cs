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
 *命名空间：SAEA.MQTT.Server
 *文件名： MqttWillDisposition
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c06
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttWillDisposition枚举
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttWillDisposition枚举
 *
 *****************************************************************************/
namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 连接断开时对 Will 消息的处置决定。
    /// </summary>
    internal enum MqttWillDisposition
    {
        /// <summary>
        /// 不发布（正常断开，或接管时被新连接取消）。
        /// </summary>
        DoNotPublish = 0,

        /// <summary>
        /// 立即发布（Will Delay 为 0 或会话立即结束）。
        /// </summary>
        PublishImmediately = 1,

        /// <summary>
        /// 延迟发布（受 Will Delay Interval 约束）。
        /// </summary>
        PublishAfterDelay = 2
    }
}