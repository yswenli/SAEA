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
 *文件名： MqttSessionExpiryManager
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c05
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttSessionExpiryManager类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttSessionExpiryManager类
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 依据 MQTT 5.0 的 Session Expiry Interval 管理会话生命周期，并按 Will Delay Interval
    /// 调度 Will 发布。连接断开时登记任务，重连、显式删除或发布完成时取消登记。
    /// </summary>
    internal sealed class MqttSessionExpiryManager : IDisposable
    {
        readonly MqttClientSessionsManager _sessionsManager;
        readonly IMqttServerOptions _options;
        readonly IMqttNetScopedLogger _logger;
        readonly MqttExpiryScheduler _scheduler;
        readonly object _syncRoot = new object();
        readonly Dictionary<string, ScheduleEntry> _schedules = new Dictionary<string, ScheduleEntry>();
        readonly Dictionary<string, ScheduleEntry> _willSchedules = new Dictionary<string, ScheduleEntry>();

        public MqttSessionExpiryManager(MqttClientSessionsManager sessionsManager, IMqttServerOptions options, IMqttNetLogger logger)
        {
            _sessionsManager = sessionsManager ?? throw new ArgumentNullException(nameof(sessionsManager));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttSessionExpiryManager));

            _scheduler = new MqttExpiryScheduler(logger);
        }

        public void Start()
        {
            _scheduler.Start();
        }

        /// <summary>
        /// 连接关闭后调用。只有代际匹配时才会登记过期任务，重复回调是幂等的。
        /// </summary>
        public void OnSessionDisconnected(
            MqttClientSession session,
            long epoch,
            MqttApplicationMessage willMessage,
            uint willDelayInterval,
            MqttWillDisposition willDisposition,
            MqttClientConnection sender)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            if (session.ConnectionEpoch != epoch)
            {
                return;
            }

            session.DisconnectedAt = MqttTime.UtcNow;

            uint effectiveExpiry = session.SessionExpiryInterval;
            if (!_options.EnablePersistentSessions)
            {
                // 保留旧开关语义：服务器不保存任何会话时，V5 也不保存。
                effectiveExpiry = 0;
            }

            if (willDisposition == MqttWillDisposition.DoNotPublish || willMessage == null)
            {
                session.CancelPendingWill();
            }
            else
            {
                session.SetPendingWill(willMessage);
            }

            var publishNow = willDisposition == MqttWillDisposition.PublishImmediately
                || (willDisposition == MqttWillDisposition.PublishAfterDelay && (willDelayInterval == 0 || effectiveExpiry == 0));

            if (publishNow)
            {
                PublishWill(session, sender);
            }
            else if (willDisposition == MqttWillDisposition.PublishAfterDelay && willMessage != null)
            {
                // 回调不能在 _syncRoot 内执行；ScheduleWill 只在锁内登记。
                ScheduleWill(session, epoch, sender, willDelayInterval);
            }

            if (effectiveExpiry == 0)
            {
                CancelWillSchedule(session.ClientId);
                CancelSchedule(session.ClientId);
                _sessionsManager.RemoveSession(session.ClientId, epoch);
                return;
            }

            if (effectiveExpiry == uint.MaxValue)
            {
                // 永不过期；will 可能已单独登记。
                CancelSchedule(session.ClientId);
                return;
            }

            ScheduleSessionExpiry(session, epoch, effectiveExpiry, sender);
        }

        /// <summary>
        /// 会话被重连复用时调用，取消尚未到期的 will 与会话删除任务。
        /// </summary>
        public void OnSessionReconnected(MqttClientSession session, long epoch)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            if (session.ConnectionEpoch != epoch)
            {
                return;
            }

            CancelWillSchedule(session.ClientId);
            CancelSchedule(session.ClientId);
            session.DisconnectedAt = null;
        }

        /// <summary>
        /// 会话被显式删除时调用，取消尚未到期的 will 与会话删除任务。
        /// </summary>
        public void OnSessionRemoved(string clientId)
        {
            if (clientId == null)
            {
                return;
            }

            CancelWillSchedule(clientId);
            CancelSchedule(clientId);
        }

        /// <summary>
        /// 测试驱动：执行所有已到期任务，无需真实等待。
        /// </summary>
        internal void RunDueTasks()
        {
            _scheduler.RunDueTasks();
        }

        /// <summary>
        /// 测试用：判断指定客户端是否仍有会话到期登记。
        /// </summary>
        internal bool IsScheduled(string clientId)
        {
            if (clientId == null)
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _schedules.ContainsKey(clientId);
            }
        }

        /// <summary>
        /// 测试用：判断指定客户端是否仍有会话到期登记。语义与 <see cref="IsScheduled"/> 一致。
        /// </summary>
        internal bool IsSessionScheduled(string clientId)
        {
            return IsScheduled(clientId);
        }

        /// <summary>
        /// 测试用：判断指定客户端是否有尚未到期的 Will 发布登记。
        /// </summary>
        internal bool IsWillScheduled(string clientId)
        {
            if (clientId == null)
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _willSchedules.ContainsKey(clientId);
            }
        }

        public void Dispose()
        {
            _scheduler.Dispose();

            lock (_syncRoot)
            {
                foreach (var entry in _schedules.Values)
                {
                    entry.Handle?.Dispose();
                }

                _schedules.Clear();

                foreach (var entry in _willSchedules.Values)
                {
                    entry.Handle?.Dispose();
                }

                _willSchedules.Clear();
            }
        }

        void ScheduleSessionExpiry(MqttClientSession session, long epoch, uint effectiveExpiry, MqttClientConnection sender)
        {
            var clientId = session.ClientId;
            var dueAt = MqttTime.UtcNow.AddSeconds(effectiveExpiry);

            lock (_syncRoot)
            {
                CancelScheduleLocked(clientId);

                var entry = new ScheduleEntry { Epoch = epoch };
                _schedules[clientId] = entry;

                entry.Handle = _scheduler.Schedule(dueAt, () =>
                {
                    try
                    {
                        if (session.ConnectionEpoch != epoch)
                        {
                            return;
                        }

                        // 会话先结束：若 will 尚未发布，先发布再删除会话。
                        PublishWill(session, sender);
                        _sessionsManager.RemoveSession(clientId, epoch);
                    }
                    finally
                    {
                        lock (_syncRoot)
                        {
                            ScheduleEntry current;
                            if (_schedules.TryGetValue(clientId, out current) && ReferenceEquals(current, entry))
                            {
                                _schedules.Remove(clientId);
                            }
                        }
                    }
                });
            }

            _logger.Verbose("Session for client '{0}' will expire in {1} seconds.", clientId, effectiveExpiry);
        }

        void ScheduleWill(MqttClientSession session, long epoch, MqttClientConnection sender, uint willDelayInterval)
        {
            var clientId = session.ClientId;
            var dueAt = MqttTime.UtcNow.AddSeconds(willDelayInterval);

            lock (_syncRoot)
            {
                CancelWillScheduleLocked(clientId);

                var entry = new ScheduleEntry { Epoch = epoch };
                _willSchedules[clientId] = entry;

                entry.Handle = _scheduler.Schedule(dueAt, () =>
                {
                    try
                    {
                        // 已被重连/新会话取代时不再发布。
                        if (session.ConnectionEpoch != epoch)
                        {
                            return;
                        }

                        PublishWill(session, sender);
                    }
                    finally
                    {
                        lock (_syncRoot)
                        {
                            ScheduleEntry current;
                            if (_willSchedules.TryGetValue(clientId, out current) && ReferenceEquals(current, entry))
                            {
                                _willSchedules.Remove(clientId);
                            }
                        }
                    }
                });
            }

            _logger.Verbose("Will for client '{0}' will be published in {1} seconds.", clientId, willDelayInterval);
        }

        void PublishWill(MqttClientSession session, MqttClientConnection sender)
        {
            MqttApplicationMessage willMessage;
            if (!session.TryTakePendingWill(out willMessage))
            {
                return;
            }

            _sessionsManager.DispatchApplicationMessage(willMessage, sender);
        }

        void CancelSchedule(string clientId)
        {
            lock (_syncRoot)
            {
                CancelScheduleLocked(clientId);
            }
        }

        void CancelScheduleLocked(string clientId)
        {
            ScheduleEntry entry;
            if (!_schedules.TryGetValue(clientId, out entry))
            {
                return;
            }

            _schedules.Remove(clientId);
            entry.Handle?.Dispose();
        }

        void CancelWillSchedule(string clientId)
        {
            lock (_syncRoot)
            {
                CancelWillScheduleLocked(clientId);
            }
        }

        void CancelWillScheduleLocked(string clientId)
        {
            ScheduleEntry entry;
            if (!_willSchedules.TryGetValue(clientId, out entry))
            {
                return;
            }

            _willSchedules.Remove(clientId);
            entry.Handle?.Dispose();
        }

        sealed class ScheduleEntry
        {
            public long Epoch;
            public IDisposable Handle;
        }
    }
}