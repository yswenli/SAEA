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
*命名空间：SAEA.QueueSocket
*文件名： PipelineWiring
*版本号： v26.11.5.1
*唯一标识：0804e9bd-ae5d-4382-a4d5-cf3505f902fb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 20:10:00
*描述：单条 QueueSocket 连接从建链到收口的装配入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：单条 QueueSocket 连接从建链到收口的装配入口
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 单条连接从建链到收口的装配入口：建通道、登记、装配处理器链、启动执行器并等待收口。
    /// <para>
    /// 启动失败时关闭流量控制并摘除登记；无论成功失败，收口后的清理都是幂等的。
    /// 本类只使用 <c>SAEA.Sockets</c> 与 <c>SAEA.Sockets.Pipeline</c> 的公开类型。
    /// </para>
    /// </summary>
    public static class PipelineWiring
    {
        /// <summary>
        /// 装配并运行一条连接，直到执行器收口完成。
        /// </summary>
        /// <param name="channelId">连接标识，为空或 null 抛 ArgumentException</param>
        /// <param name="stream">字节流接入面，为 null 抛 ArgumentNullException</param>
        /// <param name="flow">应用级流量控制闸门，为 null 抛 ArgumentNullException</param>
        /// <param name="options">通道链配置，为 null 抛 ArgumentNullException</param>
        /// <param name="transportToken">传输层断开令牌，取消时执行器转入收口</param>
        /// <param name="host">连接登记表，为 null 抛 ArgumentNullException；注册能力只在具体登记表上，故此处要求实现类型</param>
        /// <param name="configure">处理器链装配回调，接收连接标识与空链，为 null 抛 ArgumentNullException</param>
        /// <returns>执行器收口完成后结束的任务；装配或启动失败时异常原样抛出</returns>
        public static async Task RunAsync(
            string channelId,
            IRawByteStream stream,
            IReceiveFlowControl flow,
            ChannelPipelineOptions options,
            CancellationToken transportToken,
            PipelineHostRegistry host,
            Action<string, IChannelPipeline> configure)
        {
            if (string.IsNullOrEmpty(channelId))
                throw new ArgumentException("连接标识不能为空。", nameof(channelId));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            ChannelPipelineExecutor executor = null;
            ChannelPipeline pipeline = null;
            var registered = false;
            var started = false;

            try
            {
                // 关闭委托先于执行器创建：执行器尚未存在时直接关闭底层流量控制。
                var channel = new DefaultChannel(channelId, reason =>
                {
                    var current = executor;
                    return current != null ? current.BeginShutdown(reason) : flow.CloseAsync(reason);
                });

                host.Set(channel);
                registered = true;

                pipeline = ChannelPipeline.Create(options);

                executor = new ChannelPipelineExecutor(stream, channel, options, transportToken, host);
                executor.Attach(pipeline);

                // 装配固定在 Attach 之后、Start 之前：Start 一返回链结构即冻结。
                configure(channelId, pipeline);
                executor.Start();
                started = true;

                await executor.Completion.ConfigureAwait(false);
            }
            catch
            {
                if (executor != null)
                {
                    try { _ = executor.BeginShutdown("pipeline-run-failed"); } catch { }
                }

                try { await flow.CloseAsync("pipeline-run-failed").ConfigureAwait(false); } catch { }

                if (registered)
                {
                    IChannel ignored;
                    try { host.TryRemoveChannel(channelId, out ignored); } catch { }
                }

                throw;
            }
            finally
            {
                // 正常收口时执行器已摘除登记并清理属性，这里只是幂等兜底。
                if (registered)
                {
                    IChannel leftover;
                    try { host.TryRemoveChannel(channelId, out leftover); } catch { }
                }

                // 启动成功后链由执行器收口时释放；未启动成功时链还没交给执行器，由本方法释放。
                if (!started && pipeline != null)
                {
                    try { pipeline.Dispose(); } catch { }
                }
            }
        }
    }
}