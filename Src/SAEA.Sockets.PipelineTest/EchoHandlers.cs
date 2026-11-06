using System;
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