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
*文件名： MqttPacketAwaiter
*版本号： v26.4.23.1
*唯一标识：d1e5c9cb-e423-45ec-aabb-4b1b5c44f659
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPacketAwaiter接口
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
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.PacketDispatcher
{
    /// <summary>
    /// 等待指定类型应答报文的等待器，释放时从所属调度器注销
    /// </summary>
    /// <typeparam name="TPacket">期望收到的应答报文类型</typeparam>
    public sealed class MqttPacketAwaiter<TPacket> : IMqttPacketAwaiter where TPacket : MqttBasePacket
    {
        readonly TaskCompletionSource<MqttBasePacket> _taskCompletionSource;
        readonly ushort? _packetIdentifier;
        readonly MqttPacketDispatcher _owningPacketDispatcher;

        /// <summary>
        /// 创建等待器并绑定所属调度器
        /// </summary>
        /// <param name="packetIdentifier">期望应答报文的标识符，null 视为 0</param>
        /// <param name="owningPacketDispatcher">所属调度器，释放时从此处注销</param>
        public MqttPacketAwaiter(ushort? packetIdentifier, MqttPacketDispatcher owningPacketDispatcher)
        {
            _packetIdentifier = packetIdentifier;
            _owningPacketDispatcher = owningPacketDispatcher ?? throw new ArgumentNullException(nameof(owningPacketDispatcher));
#if NET452
            _taskCompletionSource = new TaskCompletionSource<MqttBasePacket>();
#else
            _taskCompletionSource = new TaskCompletionSource<MqttBasePacket>(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
        }

        /// <summary>
        /// 等待应答报文，超时则以 MqttCommunicationTimedOutException 结束等待
        /// </summary>
        /// <param name="timeout">等待超时时间</param>
        /// <returns>收到的应答报文</returns>
        public async Task<TPacket> WaitOneAsync(TimeSpan timeout)
        {
            using (var timeoutToken = new CancellationTokenSource(timeout))
            {
                using (timeoutToken.Token.Register(() => Fail(new MqttCommunicationTimedOutException())))
                {
                    var packet = await _taskCompletionSource.Task.ConfigureAwait(false);
                    return (TPacket)packet;
                }
            }
        }

        /// <summary>
        /// 用收到的报文结束等待，重复调用不生效
        /// </summary>
        /// <param name="packet">收到的应答报文</param>
        public void Complete(MqttBasePacket packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

#if NET452
            // To prevent deadlocks it is required to call the _TrySetResult_ method
            // from a new thread because the awaiting code will not(!) be executed in
            // a new thread automatically (due to await). Furthermore _this_ thread will
            // do it. But _this_ thread is also reading incoming packets -> deadlock.
            // NET452 does not support RunContinuationsAsynchronously
            Task.Run(() => _taskCompletionSource.TrySetResult(packet));
#else
            _taskCompletionSource.TrySetResult(packet);
#endif
        }

        /// <summary>
        /// 以异常结束等待，重复调用不生效
        /// </summary>
        /// <param name="exception">导致等待失败的异常</param>
        public void Fail(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
#if NET452
            // To prevent deadlocks it is required to call the _TrySetResult_ method
            // from a new thread because the awaiting code will not(!) be executed in
            // a new thread automatically (due to await). Furthermore _this_ thread will
            // do it. But _this_ thread is also reading incoming packets -> deadlock.
            // NET452 does not support RunContinuationsAsynchronously
            Task.Run(() => _taskCompletionSource.TrySetException(exception));
#else
            _taskCompletionSource.TrySetException(exception);
#endif
        }

        /// <summary>
        /// 以取消状态结束等待，重复调用不生效
        /// </summary>
        public void Cancel()
        {
#if NET452
            // To prevent deadlocks it is required to call the _TrySetResult_ method
            // from a new thread because the awaiting code will not(!) be executed in
            // a new thread automatically (due to await). Furthermore _this_ thread will
            // do it. But _this_ thread is also reading incoming packets -> deadlock.
            // NET452 does not support RunContinuationsAsynchronously
            Task.Run(() => _taskCompletionSource.TrySetCanceled());
#else
            _taskCompletionSource.TrySetCanceled();
#endif
        }

        /// <summary>
        /// 从所属调度器注销本等待器
        /// </summary>
        public void Dispose()
        {
            _owningPacketDispatcher.RemoveAwaiter<TPacket>(_packetIdentifier);
        }
    }
}