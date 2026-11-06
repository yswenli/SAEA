namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 默认流量防护实现：始终放行。
    /// </summary>
    public sealed class NullChannelGuard : IChannelGuard
    {
        /// <summary>全局共享实例。</summary>
        public static readonly NullChannelGuard Instance = new NullChannelGuard();

        private NullChannelGuard()
        {
        }

        /// <summary>始终返回 true。</summary>
        public bool Allow(in ChannelTraffic traffic)
        {
            return true;
        }
    }
}