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
*文件名： MqttPacketDispatcher
*版本号： v26.4.23.1
*唯一标识：a6ef12b8-65f1-44bc-bc28-02b320d66c52
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPacketDispatcher接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttPacketDispatcher接口
*
*****************************************************************************/
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Concurrent;

namespace SAEA.MQTT.PacketDispatcher
{
    public sealed class MqttPacketDispatcher
    {
        readonly ConcurrentDictionary<Tuple<ushort, Type>, IMqttPacketAwaiter> _awaiters = new ConcurrentDictionary<Tuple<ushort, Type>, IMqttPacketAwaiter>();

        public void Dispatch(Exception exception)
        {
            foreach (var awaiter in _awaiters)
            {
                awaiter.Value.Fail(exception);
            }

            _awaiters.Clear();
        }

        public void Dispatch(MqttBasePacket packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            if (packet is MqttDisconnectPacket disconnectPacket)
            {
                foreach (var packetAwaiter in _awaiters)
                {
                    packetAwaiter.Value.Fail(new MqttUnexpectedDisconnectReceivedException(disconnectPacket));
                }

                return;
            }

            ushort identifier = 0;
            if (packet is IMqttPacketWithIdentifier packetWithIdentifier && packetWithIdentifier.PacketIdentifier > 0)
            {
                identifier = packetWithIdentifier.PacketIdentifier;
            }

            var type = packet.GetType();
            var key = new Tuple<ushort, Type>(identifier, type);

            if (_awaiters.TryRemove(key, out var awaiter))
            {
                awaiter.Complete(packet);
                return;
            }

            throw new MqttProtocolViolationException($"Received packet '{packet}' at an unexpected time.");
        }

        public void Cancel()
        {
            foreach (var awaiter in _awaiters)
            {
                awaiter.Value.Cancel();
            }

            _awaiters.Clear();
        }

        public MqttPacketAwaiter<TResponsePacket> AddAwaiter<TResponsePacket>(ushort? identifier) where TResponsePacket : MqttBasePacket
        {
            if (!identifier.HasValue)
            {
                identifier = 0;
            }

            var awaiter = new MqttPacketAwaiter<TResponsePacket>(identifier, this);

            var key = new Tuple<ushort, Type>(identifier.Value, typeof(TResponsePacket));
            if (!_awaiters.TryAdd(key, awaiter))
            {
                throw new InvalidOperationException($"The packet dispatcher already has an awaiter for packet of type '{key.Item2.Name}' with identifier {key.Item1}.");
            }

            return awaiter;
        }

        public void RemoveAwaiter<TResponsePacket>(ushort? identifier) where TResponsePacket : MqttBasePacket
        {
            if (!identifier.HasValue)
            {
                identifier = 0;
            }

            var key = new Tuple<ushort, Type>(identifier.Value, typeof(TResponsePacket));
            _awaiters.TryRemove(key, out _);
        }
    }
}