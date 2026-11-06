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
*文件名： MqttClientSession
*版本号： v26.4.23.1
*唯一标识：5e840d6f-5b2b-450b-9d34-3601badd7756
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientSession接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 客户端会话，持有该客户端的订阅表、待投递消息队列、会话过期与遗嘱状态
    /// </summary>
    public sealed class MqttClientSession
    {
        readonly IMqttNetScopedLogger _logger;
        readonly object _willSyncRoot = new object();

        readonly DateTime _createdTimestamp = DateTime.UtcNow;
        readonly IMqttRetainedMessagesManager _retainedMessagesManager;

        /// <summary>
        /// 创建会话并使用默认的共享订阅注册表
        /// </summary>
        /// <param name="clientId">会话所属的客户端标识，不可为 null</param>
        /// <param name="items">会话级键值集合，不可为 null</param>
        /// <param name="eventDispatcher">服务端事件分发器，用于触发订阅相关回调</param>
        /// <param name="serverOptions">服务端配置</param>
        /// <param name="retainedMessagesManager">保留消息管理器，用于订阅后的补发</param>
        /// <param name="logger">根日志器，内部创建按类型隔离的作用域日志器</param>
        public MqttClientSession(
            string clientId,
            IDictionary<object, object> items,
            MqttServerEventDispatcher eventDispatcher,
            IMqttServerOptions serverOptions,
            IMqttRetainedMessagesManager retainedMessagesManager,
            IMqttNetLogger logger)
            : this(clientId, items, eventDispatcher, serverOptions, retainedMessagesManager, logger, new MqttSharedSubscriptionRegistry())
        {
        }

        internal MqttClientSession(
            string clientId,
            IDictionary<object, object> items,
            MqttServerEventDispatcher eventDispatcher,
            IMqttServerOptions serverOptions,
            IMqttRetainedMessagesManager retainedMessagesManager,
            IMqttNetLogger logger,
            MqttSharedSubscriptionRegistry sharedSubscriptionRegistry)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            _retainedMessagesManager = retainedMessagesManager ?? throw new ArgumentNullException(nameof(retainedMessagesManager));
            SubscriptionsManager = new MqttClientSubscriptionsManager(this, eventDispatcher, serverOptions, sharedSubscriptionRegistry);
            ApplicationMessagesQueue = new MqttClientSessionApplicationMessagesQueue(serverOptions);

            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttClientSession));
        }

        /// <summary>
        /// 会话所属的客户端标识
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 当前连接使用的协议版本是否为 MQTT 5.0。每次建立连接时更新。
        /// </summary>
        internal bool IsV5 { get; set; }

        /// <summary>
        /// 最近一次 CONNECT 报文申请的 CleanSession/CleanStart 标志
        /// </summary>
        public bool IsCleanSession { get; set; } = true;

        /// <summary>
        /// 旧版本使用的 Will 字段。Will 所有权已迁移到 <see cref="MqttClientConnection"/>，
        /// 此属性仅为兼容保留，不再参与运行时逻辑。
        /// </summary>
        public MqttApplicationMessage WillMessage { get; set; }

        /// <summary>
        /// 标记为“已断开、尚未决定”的悬挂 Will。
        /// </summary>
        internal void SetPendingWill(MqttApplicationMessage willMessage)
        {
            lock (_willSyncRoot)
            {
                PendingWillMessage = willMessage;
                WillPublished = false;
            }
        }

        /// <summary>
        /// 取消悬挂 Will（不发布）。
        /// </summary>
        internal void CancelPendingWill()
        {
            lock (_willSyncRoot)
            {
                PendingWillMessage = null;
                WillPublished = true;
            }
        }

        /// <summary>
        /// 原子地取出悬挂 Will；只有第一个调用者会成功。
        /// </summary>
        internal bool TryTakePendingWill(out MqttApplicationMessage willMessage)
        {
            lock (_willSyncRoot)
            {
                if (WillPublished || PendingWillMessage == null)
                {
                    willMessage = null;
                    return false;
                }

                willMessage = PendingWillMessage;
                PendingWillMessage = null;
                WillPublished = true;
                return true;
            }
        }

        /// <summary>
        /// 协商后的会话过期时间（秒）。0 表示连接关闭即结束会话；<see cref="uint.MaxValue"/> 表示永不过期。
        /// </summary>
        public uint SessionExpiryInterval { get; set; }

        /// <summary>
        /// Will 延迟时间（秒）。为 0 时连接异常断开立即发布遗嘱；
        /// 大于 0 时由 <see cref="MqttSessionExpiryManager"/> 在到期或会话结束时发布。
        /// </summary>
        public uint WillDelayInterval { get; set; }

        /// <summary>
        /// 最后建立连接的代际，用于识别过期回调是否仍属于当前连接。
        /// </summary>
        public long ConnectionEpoch { get; set; }

        /// <summary>
        /// 会话进入断开状态的时间；连接期间为 null。
        /// </summary>
        public DateTimeOffset? DisconnectedAt { get; set; }

        /// <summary>
        /// 已断开但尚未到发布时间的 will 消息；`null` 表示没有待发布的遗嘱。
        /// </summary>
        public MqttApplicationMessage PendingWillMessage { get; set; }

        /// <summary>
        /// will 是否已经被取走并发布；用于保证遗嘱最多发布一次。
        /// </summary>
        public bool WillPublished { get; set; }

        /// <summary>
        /// 该会话的订阅管理器，负责订阅增删与主题匹配
        /// </summary>
        public MqttClientSubscriptionsManager SubscriptionsManager { get; }

        /// <summary>
        /// 该会话的待投递消息队列，按 FIFO 顺序发往客户端
        /// </summary>
        public MqttClientSessionApplicationMessagesQueue ApplicationMessagesQueue { get; }

        /// <summary>
        /// 会话级键值集合，用于跨请求共享数据
        /// </summary>
        public IDictionary<object, object> Items { get; }

        /// <summary>
        /// 按会话自身订阅判断是否入队：无匹配订阅时返回 false；有匹配但消息已过期时返回 true 且不入队
        /// </summary>
        /// <param name="applicationMessage">待投递的应用消息</param>
        /// <param name="senderClientId">发布方标识，用于 NoLocal 判断；为 null 时不排除</param>
        /// <param name="isRetainedApplicationMessage">是否为订阅后的 retained 补发，决定转发时 Retain 恒为 true</param>
        /// <param name="expiresAt">消息到期时间，为 null 时由消息的 MessageExpiryInterval 推导</param>
        /// <returns>是否存在匹配订阅并已处理入队</returns>
        public bool EnqueueApplicationMessage(
            MqttApplicationMessage applicationMessage,
            string senderClientId,
            bool isRetainedApplicationMessage,
            DateTimeOffset? expiresAt = null)
        {
            var checkSubscriptionsResult = SubscriptionsManager.CheckSubscriptions(applicationMessage.Topic, applicationMessage.QualityOfServiceLevel, senderClientId);
            if (!checkSubscriptionsResult.IsSubscribed)
            {
                return false;
            }

            return EnqueueApplicationMessage(applicationMessage, senderClientId, isRetainedApplicationMessage, expiresAt, checkSubscriptionsResult.Matches);
        }

        /// <summary>
        /// 使用调用方给定的订阅匹配集合入队。多个匹配合并为一份消息：
        /// QoS 取最大，RetainAsPublished 取逻辑或，订阅标识符按匹配顺序合并去重。
        /// </summary>
        internal bool EnqueueApplicationMessage(
            MqttApplicationMessage applicationMessage,
            string senderClientId,
            bool isRetainedApplicationMessage,
            DateTimeOffset? expiresAt,
            IList<MqttSubscriptionMatch> matches)
        {
            if (matches == null || matches.Count == 0)
            {
                return false;
            }

            var qualityOfServiceLevel = matches.Max(m => m.QualityOfServiceLevel);

            var retainAsPublished = false;
            List<uint> subscriptionIdentifiers = null;

            foreach (var match in matches)
            {
                if (match.RetainAsPublished)
                {
                    retainAsPublished = true;
                }

                if (!match.SubscriptionIdentifier.HasValue)
                {
                    continue;
                }

                if (subscriptionIdentifiers == null)
                {
                    subscriptionIdentifiers = new List<uint>();
                }

                if (!subscriptionIdentifiers.Contains(match.SubscriptionIdentifier.Value))
                {
                    subscriptionIdentifiers.Add(match.SubscriptionIdentifier.Value);
                }
            }

            var now = MqttTime.UtcNow;
            var effectiveExpiresAt = expiresAt ?? MqttMessageExpiry.GetExpiresAt(applicationMessage.MessageExpiryInterval, now);

            if (effectiveExpiresAt.HasValue && now >= effectiveExpiresAt.Value)
            {
                _logger.Verbose("Skipping expired application message with topic '{0}' (ClientId: {1}).", applicationMessage.Topic, ClientId);

                // 会话已订阅但消息已过期：不进入队列，也不参与 undelivered 统计。
                return true;
            }

            _logger.Verbose("Queued application message with topic '{0}' (ClientId: {1}).", applicationMessage.Topic, ClientId);

            ApplicationMessagesQueue.Enqueue(
                applicationMessage,
                senderClientId,
                qualityOfServiceLevel,
                isRetainedApplicationMessage,
                effectiveExpiresAt,
                retainAsPublished,
                subscriptionIdentifiers);

            return true;
        }

        /// <summary>
        /// 对订阅成功后需要补发的过滤器补发匹配的 retained 消息。
        /// 共享订阅过滤器不会出现在 <paramref name="replayFilters"/> 中；若出现也会被跳过。
        /// </summary>
        internal async Task EnqueueRetainedMessagesAsync(ICollection<MqttTopicFilter> replayFilters)
        {
            if (replayFilters == null || replayFilters.Count == 0)
            {
                return;
            }

            var matchingRetainedMessages = await _retainedMessagesManager.GetSubscribedMessageEntriesAsync(replayFilters).ConfigureAwait(false);
            foreach (var matchingRetainedMessage in matchingRetainedMessages)
            {
                if (matchingRetainedMessage?.Message == null)
                {
                    continue;
                }

                var matches = new List<MqttSubscriptionMatch>();

                foreach (var filter in replayFilters)
                {
                    if (filter == null || filter.ShareName != null)
                    {
                        continue;
                    }

                    var effectiveFilter = filter.ParsedFilter ?? filter.Topic;
                    if (string.IsNullOrEmpty(effectiveFilter) ||
                        !MqttTopicFilterComparer.IsMatch(matchingRetainedMessage.Message.Topic, effectiveFilter))
                    {
                        continue;
                    }

                    matches.Add(new MqttSubscriptionMatch
                    {
                        Filter = effectiveFilter,
                        QualityOfServiceLevel = Min(matchingRetainedMessage.Message.QualityOfServiceLevel, filter.QualityOfServiceLevel),
                        // retained 补发必须 Retain=true，因此不保留原始标志；由 isRetainedApplicationMessage 保证。
                        RetainAsPublished = false,
                        RetainHandling = filter.RetainHandling ?? MqttRetainHandling.SendAtSubscribe,
                        SubscriptionIdentifier = filter.SubscriptionIdentifier,
                        IsShared = false,
                        ShareName = null
                    });
                }

                // senderClientId 传 null，使 NoLocal 不排除 retained 补发。
                EnqueueApplicationMessage(matchingRetainedMessage.Message, null, true, matchingRetainedMessage.ExpiresAt, matches);
            }
        }

        /// <summary>
        /// 以服务端身份为会话添加订阅，并补发匹配的 retained 消息；topicFilters 为 null 时抛出异常
        /// </summary>
        /// <param name="topicFilters">要添加的订阅过滤器集合</param>
        /// <returns>订阅与 retained 补发完成的任务</returns>
        public async Task SubscribeAsync(ICollection<MqttTopicFilter> topicFilters)
        {
            if (topicFilters is null) throw new ArgumentNullException(nameof(topicFilters));

            var replayFilters = await SubscriptionsManager.SubscribeAsync(topicFilters).ConfigureAwait(false);
            await EnqueueRetainedMessagesAsync(replayFilters).ConfigureAwait(false);
        }

        /// <summary>
        /// 移除会话的订阅，topicFilters 为 null 时抛出异常
        /// </summary>
        /// <param name="topicFilters">要移除的订阅主题集合</param>
        /// <returns>取消订阅完成的任务</returns>
        public Task UnsubscribeAsync(IEnumerable<string> topicFilters)
        {
            if (topicFilters is null) throw new ArgumentNullException(nameof(topicFilters));

            return SubscriptionsManager.UnsubscribeAsync(topicFilters);
        }

        /// <summary>
        /// 把会话标识、创建时间、待投递数量与键值集合写入状态对象，供状态查询使用
        /// </summary>
        /// <param name="status">待填充的会话状态对象，其字段会被整体覆盖</param>
        public void FillStatus(MqttSessionStatus status)
        {
            status.ClientId = ClientId;
            status.CreatedTimestamp = _createdTimestamp;
            status.PendingApplicationMessagesCount = ApplicationMessagesQueue.Count;
            status.Items = Items;
        }

        static MqttQualityOfServiceLevel Min(MqttQualityOfServiceLevel left, MqttQualityOfServiceLevel right)
        {
            return (int)left <= (int)right ? left : right;
        }
    }
}