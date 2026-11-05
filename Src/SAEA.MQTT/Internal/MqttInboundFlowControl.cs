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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttInboundFlowControl
 *版本号： v26.4.23.1
 *唯一标识：5f7c0a3e-9c41-4d2b-8f61-2a7e4c8b1105
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttInboundFlowControl类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttInboundFlowControl类
 *
 *****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 每连接一份的入站 Receive Maximum 配额。QoS0 不占配额，重复的数据包标识符（DUP 重发）不重复扣减。
    /// </summary>
    internal sealed class MqttInboundFlowControl
    {
        readonly object _syncRoot = new object();
        readonly HashSet<ushort> _packetIdentifiers = new HashSet<ushort>();
        readonly ushort _receiveMaximum;

        public MqttInboundFlowControl(ushort receiveMaximum)
        {
            // 0 在协议上非法，这里做防御性归一化，避免所有 QoS>0 报文被拒绝。
            _receiveMaximum = receiveMaximum == 0 ? ushort.MaxValue : receiveMaximum;
        }

        public bool TryAcquire(ushort packetIdentifier, MqttQualityOfServiceLevel qos)
        {
            if (qos == MqttQualityOfServiceLevel.AtMostOnce)
            {
                return true;
            }

            lock (_syncRoot)
            {
                if (_packetIdentifiers.Contains(packetIdentifier))
                {
                    // DUP 重发同一数据包标识符，已经计过数了。
                    return true;
                }

                if (_packetIdentifiers.Count >= _receiveMaximum)
                {
                    return false;
                }

                _packetIdentifiers.Add(packetIdentifier);
                return true;
            }
        }

        public void Release(ushort packetIdentifier)
        {
            lock (_syncRoot)
            {
                _packetIdentifiers.Remove(packetIdentifier);
            }
        }

        public int Available
        {
            get
            {
                lock (_syncRoot)
                {
                    var available = _receiveMaximum - _packetIdentifiers.Count;
                    return available < 0 ? 0 : available;
                }
            }
        }
    }
}