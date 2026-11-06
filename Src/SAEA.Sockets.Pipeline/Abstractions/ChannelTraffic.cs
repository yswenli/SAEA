namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接级流量的只读快照。三个值各自原子读取，不保证严格同步，
    /// 仅用于防护判断，不作为精确账本。
    /// </summary>
    public readonly struct ChannelTraffic
    {
        /// <summary>本连接累计接收的字节数，只增不减。</summary>
        public long TotalReceivedBytes { get; }

        /// <summary>当前已缓冲但尚未成帧的残余字节数。</summary>
        public long BufferedBytes { get; }

        /// <summary>当前队列中的消息数。</summary>
        public int QueuedMessages { get; }

        /// <summary>创建一份流量快照。</summary>
        public ChannelTraffic(long totalReceivedBytes, long bufferedBytes, int queuedMessages)
        {
            TotalReceivedBytes = totalReceivedBytes;
            BufferedBytes = bufferedBytes;
            QueuedMessages = queuedMessages;
        }
    }
}