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
*文件名： NullChannelGuard
*版本号： v26.11.5.1
*唯一标识：ababfba6-4285-4d7f-a47f-e7795d822237
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：默认流量防护实现：始终放行
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：默认流量防护实现：始终放行
*
*****************************************************************************/
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
        /// <param name="traffic">当前连接的流量快照，本实现忽略该参数。</param>
        /// <returns>始终为 true。</returns>
        public bool Allow(in ChannelTraffic traffic)
        {
            return true;
        }
    }
}