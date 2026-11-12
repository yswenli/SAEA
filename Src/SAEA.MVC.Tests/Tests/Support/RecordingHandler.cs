/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Tests.Support
*文件名： RecordingHandler
*版本号： net10.0
*唯一标识：b9ae96c6-aa01-4430-9e9c-7227d6bbf2d7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：记录链活动/失活通知的入站处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:05:27
*修改人： yswenli
*版本号： net10.0
*描述：新增，用计数断言装配回调每条连接只执行一次
*
*****************************************************************************/
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets.Pipeline;

namespace SAEA.MVC.Tests.Tests.Support
{
    /// <summary>
    /// 统计链活动与失活通知次数的入站处理器：同一实例可挂到多条链，计数按连接累计。
    /// </summary>
    public sealed class RecordingHandler : InboundHandlerAdapter
    {
        private int _channelActiveCount;

        private int _channelInactiveCount;

        /// <summary>
        /// 允许同一实例挂到多条连接链，计数按连接范围累计。
        /// </summary>
        public override bool IsSharable
        {
            get { return true; }
        }

        /// <summary>
        /// 收到的链活动通知次数。
        /// </summary>
        public int ChannelActiveCount
        {
            get { return Volatile.Read(ref _channelActiveCount); }
        }

        /// <summary>
        /// 收到的链失活通知次数。
        /// </summary>
        public int ChannelInactiveCount
        {
            get { return Volatile.Read(ref _channelInactiveCount); }
        }

        /// <summary>
        /// 记录一次链活动通知。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <returns>已完成的任务</returns>
        public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
        {
            Interlocked.Increment(ref _channelActiveCount);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 记录一次链失活通知。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <returns>已完成的任务</returns>
        public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
        {
            Interlocked.Increment(ref _channelInactiveCount);

            return Task.CompletedTask;
        }
    }
}