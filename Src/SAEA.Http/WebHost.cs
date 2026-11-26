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
 *命名空间：SAEA.Http
 *文件名： WebHost
 *版本号： v26.4.23.1
 *唯一标识：e59016b9-6d64-4c89-8826-f139a38be098
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2018/10/28 14:19:50
 *描述：WebHost类
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 16:21:35
*修改人： yswenli
*版本号： v26.11.26.1
*描述：新增接收 ReadOnlyMemory 的 SendAsync，大数据段发送不再复制成独立数组
*
*****************************************************************************/
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA Web 主机，组装 HttpSocket、WebConfig 与 HttpUtility 并对外提供收发能力
    /// </summary>
    public class WebHost : IWebHost
    {
        IHttpSocket _httpServer;

        private readonly int _port;

        private readonly int _bufferSize;

        private readonly int _maxConnects;

        private readonly double _timeout;

        private readonly double _connectTimeout;

        private readonly bool _isDebug;

        Type _httpContentType = typeof(HttpContext);

        // 上下文构造委托：Activator.CreateInstance 每请求都要走一次类型查找与反射激活，这里按类型建一次缓存
        Func<IWebHost, IHttpContext> _contextFactory;

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        ///  Web 服务器配置
        /// </summary>
        public WebConfig WebConfig { get; set; }

        /// <summary>
        /// Http 服务器实用工具，负责静态资源路径映射
        /// </summary>
        public HttpUtility HttpUtility
        {
            get;
            private set;
        }

        /// <summary>
        /// 路由扩展参数，由业务自行解释
        /// </summary>
        public object RouteParam { get; set; }


        /// <summary>
        /// 创建 Web 主机，按参数初始化配置与静态资源工具；底层 socket 延迟到 Start 时创建，
        /// 便于在构造之后、启动之前配置 TLS 等只允许启动前设置的项目。
        /// </summary>
        /// <param name="httpContentType">自定义 IHttpContext 实现类型，null 或不合规时用内置 HttpContext</param>
        /// <param name="root">站点根目录名，默认为 wwwroot</param>
        /// <param name="port">监听端口，默认 39654</param>
        /// <param name="isStaticsCached">是否启用静态资源缓存</param>
        /// <param name="isZiped">是否启用 gzip 内容压缩</param>
        /// <param name="bufferSize">接收缓冲区字节数，默认 64KB</param>
        /// <param name="maxConnects">最大连接数，默认 1000</param>
        /// <param name="timeout">连接空闲超时秒数，默认 180</param>
        /// <param name="connectTimeout">连接建立超时秒数，默认 3</param>
        /// <param name="isDebug">是否输出调试日志</param>
        public WebHost(Type httpContentType = null,
            string root = "wwwroot",
            int port = 39654,
            bool isStaticsCached = true,
            bool isZiped = true,
            int bufferSize = 64 * 1024,
            int maxConnects = 1000,
            double timeout = 180,
            double connectTimeout = 3,
            bool isDebug = false)
        {
            // 只接受实现了 IHttpContext 的类型：校验对象必须是入参，字段此刻仍是默认值，
            // 用字段校验会让任何类型都通过，随后在预编译构造委托时抛出难以定位的表达式异常
            if (httpContentType != null && httpContentType.GetInterface("SAEA.Http.Model.IHttpContext", true) != null)
            {
                _httpContentType = httpContentType;
            }
            else
            {
                _httpContentType = typeof(HttpContext);
            }

            _contextFactory = CreateContextFactory(_httpContentType);

            WebConfig = new WebConfig()
            {
                Root = root,
                Port = port,
                IsStaticsCached = isStaticsCached,
                IsZiped = isZiped,
                HandleBufferSize = bufferSize,
                MaxConnects = maxConnects,
                Timeout = timeout
            };

            HttpUtility = new HttpUtility(WebConfig.Root);

            _port = port;
            _bufferSize = bufferSize;
            _maxConnects = maxConnects;
            _timeout = timeout;
            _connectTimeout = connectTimeout;
            _isDebug = isDebug;
        }

        /// <summary>
        /// 按配置的上下文类型创建一次请求的业务上下文。
        /// </summary>
        /// <returns>新建的业务上下文</returns>
        internal IHttpContext CreateContext()
        {
            return _contextFactory(this);
        }

        /// <summary>
        /// 按上下文类型预编译构造委托并缓存，避免每请求走 Activator 的反射激活流程
        /// </summary>
        /// <param name="contextType">IHttpContext 实现类型</param>
        /// <returns>接收宿主实例的构造委托</returns>
        static Func<IWebHost, IHttpContext> CreateContextFactory(Type contextType)
        {
            var constructor = contextType.GetConstructor(new[] { typeof(IWebHost) });

            // 没有 (IWebHost) 构造时退回 Activator，保持原有的失败时机与异常类型
            if (constructor == null)
                return webHost => (IHttpContext)Activator.CreateInstance(contextType, webHost);

            var webHostParam = Expression.Parameter(typeof(IWebHost), "webHost");

            var body = Expression.Convert(Expression.New(constructor, webHostParam), typeof(IHttpContext));

            return Expression.Lambda<Func<IWebHost, IHttpContext>>(body, webHostParam).Compile();
        }


        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                // 先冻结再监听：接受连接可能在 Start 返回前发生，冻结必须早于第一个连接建链。
                WebConfig.FreezeConfigurePipeline();
                WebConfig.FreezeSsl();

                // socket 到启动时才创建，让 TLS 配置在构造之后、Start 之前仍可设置。
                if (_httpServer == null)
                    _httpServer = new HttpSocket(this, _port, _bufferSize, _maxConnects, _timeout, _connectTimeout, _isDebug, WebConfig.ConfigureSsl);

                _httpServer.Start();
                IsRunning = true;
            }
        }


        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public void SetCrossDomainHeaders(params string[] headers)
        {
            ConstHelper.SetCrossDomainHeaders(headers);
        }
        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public void SetCrossDomainHeaders(string headers)
        {
            ConstHelper.SetCrossDomainHeaders(headers);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送字节</param>
        public void Send(IChannel channel, byte[] data)
        {
            SendAsync(channel, data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步发送，不关闭连接；写失败只记日志，不向调用方抛出
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送字节</param>
        /// <returns>写出完成后的任务</returns>
        public Task SendAsync(IChannel channel, byte[] data)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
                return Task.CompletedTask;

            return session.WriteBytesSafeAsync(data);
        }

        /// <summary>
        /// 异步发送一段字节（可以是池化缓冲中的一段），不关闭连接；写失败只记日志，不向调用方抛出
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送字节</param>
        /// <returns>写出完成后的任务</returns>
        internal Task SendAsync(IChannel channel, ReadOnlyMemory<byte> data)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
                return Task.CompletedTask;

            return session.WriteBytesSafeAsync(data);
        }

        /// <summary>
        /// 异步发送池化帧，不关闭连接；缓冲所有权随帧移交，写完成、失败或拒绝时都由接收方释放
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送的完整帧</param>
        /// <param name="owner">背衬缓冲的归还句柄</param>
        /// <returns>写出完成后的任务</returns>
        internal Task SendFrameAsync(IChannel channel, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
            {
                // 没有会话就没有接收方，缓冲当场归还，不能让调用方背着一个池化租约收工
                owner.Dispose();
                return Task.CompletedTask;
            }

            return session.WriteFrameSafeAsync(data, owner);
        }

        /// <summary>
        /// 结束
        /// </summary>
        /// <param name="channel">连接通道</param>
        public void Disconnect(IChannel channel)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
                return;

            session.Close();
        }

        /// <summary>
        /// 设置/取消会话的空闲超时豁免
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="noTimeout">true 表示不参与空闲清理</param>
        public void SetNoTimeout(string id, bool noTimeout)
        {
            // 未启动时底层 socket 还没创建，没有会话需要豁免。
            var httpServer = _httpServer;
            if (httpServer == null)
                return;

            httpServer.SetNoTimeout(id, noTimeout);
        }

        /// <summary>
        /// 发送并结束
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送字节</param>
        public void End(IChannel channel, byte[] data)
        {
            EndAsync(channel, data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步发送并在写完成后关闭连接；写失败只记日志，不向调用方抛出
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送字节</param>
        /// <returns>写出完成并已提交关闭后的任务</returns>
        public Task EndAsync(IChannel channel, byte[] data)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
                return Task.CompletedTask;

            return session.WriteBytesAndCloseAsync(data);
        }

        /// <summary>
        /// 异步发送池化帧并在写完成后关闭连接；缓冲所有权随帧移交，关闭与否都会归还
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="data">待发送的完整帧</param>
        /// <param name="owner">背衬缓冲的归还句柄</param>
        /// <returns>写出完成并已提交关闭后的任务</returns>
        internal Task EndFrameAsync(IChannel channel, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null)
            {
                owner.Dispose();
                return Task.CompletedTask;
            }

            return session.WriteFrameAndCloseAsync(data, owner);
        }

        /// <summary>
        /// 重启
        /// </summary>
        public void Restart()
        {
            this.Stop();
            this.Start();
        }

        /// <summary>
        /// 关闭服务
        /// </summary>
        public void Stop()
        {
            if (IsRunning && _httpServer != null)
            {
                _httpServer.Stop();
            }

            // 释放旧 socket：Restart 会在 Start 里按当前配置（含 TLS）重新创建。
            _httpServer = null;
            IsRunning = false;
        }


    }
}