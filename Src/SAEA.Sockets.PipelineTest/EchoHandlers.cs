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
*命名空间：SAEA.Sockets.PipelineTest
*文件名： EchoHandlers
*版本号： v26.11.5.1
*唯一标识：411cca2c-31c2-4bbd-8e62-87547fa7421e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：登录处理器：模拟 30ms 鉴权延迟，成功后写入连接属性并通过当前节点回包
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：登录处理器：模拟 30ms 鉴权延迟，成功后写入连接属性并通过当前节点回包
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.PipelineTest
{
    /// <summary>
    /// 登录处理器：模拟 30ms 鉴权延迟，成功后写入连接属性并通过当前节点回包。
    /// 回包必须使用消息分发器传入的 <paramref name="ctx"/>，不能另建指向链首的上下文。
    /// </summary>
    public sealed class LoginHandler : IMessageHandler
    {
        /// <summary>默认鉴权延迟（毫秒）。</summary>
        public const int DefaultDelayMilliseconds = 30;

        private readonly int _delayMilliseconds;

        private int _handled;

        private LoginResponse _lastResponse;

        /// <summary>使用默认 30ms 鉴权延迟。</summary>
        public LoginHandler()
            : this(DefaultDelayMilliseconds)
        {
        }

        /// <summary>指定鉴权延迟；0 表示不等待。</summary>
        public LoginHandler(int delayMilliseconds)
        {
            if (delayMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "延迟不能为负。");

            _delayMilliseconds = delayMilliseconds;
        }

        /// <summary>最近一次回包实体，供演示用例做手工编码比对。</summary>
        public LoginResponse LastResponse
        {
            get { return Volatile.Read(ref _lastResponse); }
        }

        /// <summary>已处理的登录请求数。</summary>
        public int HandledCount
        {
            get { return Volatile.Read(ref _handled); }
        }

        /// <inheritdoc />
        public async Task HandleAsync(IChannelHandlerContext ctx, object message)
        {
            var request = message as LoginRequest;
            if (request == null)
                throw new InvalidOperationException("LoginHandler 只接受 LoginRequest。");

            if (_delayMilliseconds > 0)
                await Task.Delay(_delayMilliseconds, ctx.CancellationToken).ConfigureAwait(false);

            var response = CreateResponse(request);
            Volatile.Write(ref _lastResponse, response);
            Interlocked.Increment(ref _handled);

            // 登录成功后放行后续类型，并写入第二级鉴权需要的令牌属性。
            ctx.Channel.SetAttribute(AuthHandler.AuthedAttributeKey, true);
            ctx.Channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, response.Token);

            await ctx.WriteAsync(response).ConfigureAwait(false);
        }

        /// <summary>按请求构造响应；出站字节比对用例复用同一方法保证字段一致。</summary>
        public static LoginResponse CreateResponse(LoginRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var userName = string.IsNullOrEmpty(request.UserName) ? "anonymous" : request.UserName;

            return new LoginResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Token = "token-" + userName,
                UserName = userName
            };
        }
    }

    /// <summary>
    /// 慢鉴权替身：按心跳序号延迟不同时长，记录完成顺序与峰值并发。
    /// 用于演示「同连接消息并发处理、回包顺序可以不同于到达顺序」；
    /// 真实项目里这段等待应发生在鉴权服务调用处，而不是链上的固定延时。
    /// </summary>
    public sealed class DelayedAuthHandler : InboundHandlerAdapter
    {
        private readonly int[] _delays;

        private readonly ConcurrentQueue<int> _completionOrder = new ConcurrentQueue<int>();

        private int _active;

        private int _peak;

        private int _completed;

        /// <summary>按序号顺序给出每条消息的延迟（毫秒），序号 1 对应下标 0，越界时取最后一个。</summary>
        public DelayedAuthHandler(params int[] delays)
        {
            if (delays == null || delays.Length == 0)
                throw new ArgumentException("至少需要一个延迟值。", nameof(delays));

            _delays = delays;
        }

        /// <summary>完成顺序（心跳序号）。</summary>
        public int[] CompletionOrder
        {
            get { return _completionOrder.ToArray(); }
        }

        /// <summary>观测到的峰值并发。</summary>
        public int PeakConcurrency
        {
            get { return Volatile.Read(ref _peak); }
        }

        /// <summary>已完成消息数。</summary>
        public int Completed
        {
            get { return Volatile.Read(ref _completed); }
        }

        /// <inheritdoc />
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var heartbeat = msg as Heartbeat;
            if (heartbeat == null)
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                return;
            }

            var active = Interlocked.Increment(ref _active);
            while (true)
            {
                var peak = Volatile.Read(ref _peak);
                if (active <= peak || Interlocked.CompareExchange(ref _peak, active, peak) == peak)
                    break;
            }

            try
            {
                var index = Math.Min(_delays.Length - 1, Math.Max(0, heartbeat.Seq - 1));
                if (_delays[index] > 0)
                    await Task.Delay(_delays[index], ctx.CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }

            _completionOrder.Enqueue(heartbeat.Seq);
            Interlocked.Increment(ref _completed);

            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 心跳处理器：立即把同一序号回显回去，不做任何等待。
    /// </summary>
    public sealed class HeartbeatHandler : IMessageHandler
    {
        private int _handled;

        private Heartbeat _lastResponse;

        /// <summary>最近一次回包实体，供演示用例做手工编码比对。</summary>
        public Heartbeat LastResponse
        {
            get { return Volatile.Read(ref _lastResponse); }
        }

        /// <summary>已处理的心跳数。</summary>
        public int HandledCount
        {
            get { return Volatile.Read(ref _handled); }
        }

        /// <inheritdoc />
        public async Task HandleAsync(IChannelHandlerContext ctx, object message)
        {
            var heartbeat = message as Heartbeat;
            if (heartbeat == null)
                throw new InvalidOperationException("HeartbeatHandler 只接受 Heartbeat。");

            var response = new Heartbeat
            {
                Seq = heartbeat.Seq,
                Payload = heartbeat.Payload
            };

            Volatile.Write(ref _lastResponse, response);
            Interlocked.Increment(ref _handled);

            await ctx.WriteAsync(response).ConfigureAwait(false);
        }
    }
}