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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Concurrent;

namespace SAEA.MQTT.PacketDispatcher
{
    /// <summary>
    /// 报文调度器，按「包标识符 + 报文类型」把应答报文交给对应的等待器
    /// </summary>
    public sealed class MqttPacketDispatcher
    {
        readonly ConcurrentDictionary<Tuple<ushort, Type>, IMqttPacketAwaiter> _awaiters = new ConcurrentDictionary<Tuple<ushort, Type>, IMqttPacketAwaiter>();

        /// <summary>
        /// 以异常结束全部等待并清空等待器
        /// </summary>
        /// <param name="exception">传递给各等待器的异常</param>
        public void Dispatch(Exception exception)
        {
            foreach (var awaiter in _awaiters)
            {
                awaiter.Value.Fail(exception);
            }

            _awaiters.Clear();
        }

        /// <summary>
        /// 把收到的报文交给匹配的等待器；收到 DISCONNECT 时让全部等待器失败，无匹配等待器时抛出协议违约异常
        /// </summary>
        /// <param name="packet">收到的报文</param>
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

        /// <summary>
        /// 以取消状态结束全部等待并清空等待器
        /// </summary>
        public void Cancel()
        {
            foreach (var awaiter in _awaiters)
            {
                awaiter.Value.Cancel();
            }

            _awaiters.Clear();
        }

        /// <summary>
        /// 注册等待器
        /// </summary>
        /// <typeparam name="TResponsePacket">期望收到的应答报文类型</typeparam>
        /// <param name="identifier">报文标识符，null 视为 0</param>
        /// <returns>新注册的等待器</returns>
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

        /// <summary>
        /// 注销等待器，不存在时忽略
        /// </summary>
        /// <typeparam name="TResponsePacket">等待的应答报文类型</typeparam>
        /// <param name="identifier">报文标识符，null 视为 0</param>
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