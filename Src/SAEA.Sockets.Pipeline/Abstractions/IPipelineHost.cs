/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： IPipelineHost
*版本号： v26.11.5.1
*唯一标识：b957b15e-7c01-4aea-ba3e-95b70b2a6234
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：连接登记表：按连接标识查询应用级通道视图
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：连接登记表：按连接标识查询应用级通道视图
*
*****************************************************************************/
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