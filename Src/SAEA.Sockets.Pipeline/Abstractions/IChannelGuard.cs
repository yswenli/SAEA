namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接级流量防护钩子，每次读循环迭代检查一次。
    /// </summary>
    public interface IChannelGuard
    {
        /// <summary>
        /// 返回 false 表示超限，调用方以 "guard-rejected" 关闭连接。
        /// 钩子自身抛异常时按超限处理并记录，不放行。
        /// </summary>
        bool Allow(in ChannelTraffic traffic);
    }
}