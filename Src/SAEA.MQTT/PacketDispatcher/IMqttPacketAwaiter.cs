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
*命名空间：SAEA.MQTT.PacketDispatcher
*文件名： IMqttPacketAwaiter
*版本号： v26.4.23.1
*唯一标识：cd106aee-18cd-4f16-8bb9-816f6cbba43f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttPacketAwaiter接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Packets;
using System;

namespace SAEA.MQTT.PacketDispatcher
{
    /// <summary>
    /// 报文等待器，用于让发起请求的一方异步等待指定类型的应答报文
    /// </summary>
    public interface IMqttPacketAwaiter : IDisposable
    {
        /// <summary>
        /// 用收到的报文结束等待
        /// </summary>
        /// <param name="packet">收到的应答报文</param>
        void Complete(MqttBasePacket packet);

        /// <summary>
        /// 以异常结束等待
        /// </summary>
        /// <param name="exception">导致等待失败的异常</param>
        void Fail(Exception exception);

        /// <summary>
        /// 以取消状态结束等待
        /// </summary>
        void Cancel();
    }
}