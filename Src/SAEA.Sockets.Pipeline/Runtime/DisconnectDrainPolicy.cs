namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接进入收口后对在途消息的处理策略。队列内尚未开始的消息两种策略都丢弃并归还。
    /// </summary>
    public enum DisconnectDrainPolicy
    {
        /// <summary>
        /// 网关场景：断开即放弃。在途消息立即取消，队列内未开始的丢弃并归还。
        /// </summary>
        CancelPending = 0,

        /// <summary>
        /// 业务场景：断开后让已开始的消息自然跑完，队列内未开始的丢弃并归还。
        /// <para>
        /// "跑完" = 让已开始的业务副作用完成，不代表回包能送达对端。
        /// 该策略必须配合 <see cref="ChannelPipelineOptions.DrainTimeout"/> 兜底，否则卡死的处理器会让连接永远回收不了。
        /// </para>
        /// </summary>
        DrainInFlight = 1
    }
}