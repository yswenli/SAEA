using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接登记表：按连接标识查询应用级通道视图。
    /// 由传输适配层实现并持有，连接建立时写入、收口时移除。
    /// </summary>
    public interface IPipelineHost
    {
        /// <summary>按连接标识查询通道；未建链或已关闭返回 false。</summary>
        bool TryGetChannel(string channelId, out IChannel channel);

        /// <summary>
        /// 解除登记并返回该通道；不存在返回 false，幂等。
        /// 实现应同时把通道置为非活跃并清空属性。
        /// </summary>
        bool TryRemoveChannel(string channelId, out IChannel channel);

        /// <summary>等价于通道的 CloseAsync；连接不存在时为 no-op，幂等。</summary>
        Task CloseAsync(string channelId, string reason);
    }
}