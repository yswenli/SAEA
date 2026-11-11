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
*命名空间：SAEA.P2P.Turn
*文件名： UdpTurnClientTransport
*版本号： v26.11.5.1
*唯一标识：59cbe29c-f0e7-493e-af49-7e846a51b5cd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 04:10:00
*描述：TURN over UDP 客户端控制传输
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 04:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN over UDP 客户端控制传输
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN over UDP 客户端控制传输：一个数据报就是一帧，不做流式分帧。
    /// 自建实例拥有底层 UDP 传输并在打开时绑定；注入实例不拥有底层传输，释放时只退订接收事件。
    /// </summary>
    public sealed class UdpTurnClientTransport : ITurnClientTransport
    {
        /// <summary>
        /// 底层 UDP 传输
        /// </summary>
        private readonly IUdpTransport _udp;

        /// <summary>
        /// 是否拥有底层传输；注入实例为 false，释放时不关闭它
        /// </summary>
        private readonly bool _ownsTransport;

        /// <summary>
        /// 打开时使用的本地端点；注入实例为 null，由注入方自行绑定
        /// </summary>
        private readonly IPEndPoint _localBindEndPoint;

        /// <summary>
        /// TURN 服务端端点
        /// </summary>
        private readonly IPEndPoint _serverEndPoint;

        /// <summary>
        /// 保护打开与释放状态的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 是否已完成打开流程
        /// </summary>
        private bool _opened;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 收到一整帧时触发，参数是副本，可在回调返回后继续持有
        /// </summary>
        public event Action<byte[]> FrameReceived;

        /// <summary>
        /// 创建并拥有底层 UDP 传输；构造阶段不绑定，由 <see cref="OpenAsync"/> 打开
        /// </summary>
        /// <param name="localEndPoint">本地绑定端点；为 null 时绑定 <see cref="IPAddress.Any"/> 的随机端口</param>
        /// <param name="serverEndPoint">TURN 服务端端点</param>
        /// <exception cref="ArgumentNullException">serverEndPoint 为 null</exception>
        public UdpTurnClientTransport(IPEndPoint localEndPoint, IPEndPoint serverEndPoint)
        {
            if (serverEndPoint == null)
            {
                throw new ArgumentNullException(nameof(serverEndPoint));
            }

            _serverEndPoint = serverEndPoint;
            _localBindEndPoint = localEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
            _udp = new SaeaUdpTransport();
            _ownsTransport = true;
            _udp.DatagramReceived += OnDatagramReceived;
        }

        /// <summary>
        /// 包装调用方已创建的 UDP 传输；本实例不拥有也不绑定它，释放时只退订接收事件
        /// </summary>
        /// <param name="udp">已绑定或由调用方负责绑定的 UDP 传输</param>
        /// <param name="serverEndPoint">TURN 服务端端点</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public UdpTurnClientTransport(IUdpTransport udp, IPEndPoint serverEndPoint)
        {
            if (udp == null)
            {
                throw new ArgumentNullException(nameof(udp));
            }
            if (serverEndPoint == null)
            {
                throw new ArgumentNullException(nameof(serverEndPoint));
            }

            _serverEndPoint = serverEndPoint;
            _udp = udp;
            _ownsTransport = false;
            _udp.DatagramReceived += OnDatagramReceived;
        }

        /// <summary>
        /// 本地绑定的端点；尚未打开或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _disposed ? null : _udp.LocalEndPoint; }
        }

        /// <summary>
        /// TURN 服务端端点
        /// </summary>
        public IPEndPoint ServerEndPoint
        {
            get { return _serverEndPoint; }
        }

        /// <summary>
        /// 是否为可靠传输；UDP 恒为 false
        /// </summary>
        public bool IsReliable
        {
            get { return false; }
        }

        /// <summary>
        /// 是否已打开并可收发
        /// </summary>
        public bool IsConnected
        {
            get { return !_disposed && _udp.IsConnected; }
        }

        /// <summary>
        /// 打开传输：自建实例先绑定本地端点，再启动接收；注入实例只启动接收，绑定由注入方负责
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>打开完成的任务</returns>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                if (_opened)
                {
                    return;
                }

                // 必须在 await 之前占位，否则并发调用会各自绑定一次
                _opened = true;
            }

            try
            {
                if (_ownsTransport)
                {
                    await _udp.BindAsync(_localBindEndPoint, cancellationToken).ConfigureAwait(false);
                }

                _udp.StartReceive();
            }
            catch
            {
                // 任何失败都要复位，否则该实例会永久拒绝重试
                lock (_sync)
                {
                    _opened = false;
                }
                throw;
            }
        }

        /// <summary>
        /// 发送一整帧；失败只记调试日志，由 TURN 客户端按事务超时失败
        /// </summary>
        /// <param name="frame">完整帧字节，不得为 null</param>
        /// <exception cref="ArgumentNullException">frame 为 null</exception>
        public void Send(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }
            if (_disposed)
            {
                return;
            }

            try
            {
                var sending = _udp.SendToAsync(_serverEndPoint, new ReadOnlyMemory<byte>(frame), CancellationToken.None);
                ObserveSendFailure(sending);
            }
            catch (Exception ex)
            {
                // 发送失败不向调用方抛，避免把传输错误散布到 TURN 事务层之外
                P2PLogHelper.Debug("UdpTurnClientTransport", "发送失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 退订接收事件；自建实例同时释放底层传输，注入实例只退订
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            try
            {
                _udp.DatagramReceived -= OnDatagramReceived;
            }
            catch (ObjectDisposedException)
            {
                // 底层已释放时退订为空操作
            }

            if (!_ownsTransport)
            {
                return;
            }

            try
            {
                _udp.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("UdpTurnClientTransport", "释放 UDP 传输失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 底层接收回调：复制数据报后触发帧事件；来源校验由 TURN 客户端按事务 ID 与通道号完成
        /// </summary>
        /// <param name="remoteEndPoint">数据报来源端点</param>
        /// <param name="data">数据报内容，仅在回调期间有效</param>
        private void OnDatagramReceived(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
        {
            // 回调返回后内存不再有效，必须复制；空数据报复制成空数组，不影响后续接收
            RaiseFrameReceived(data.ToArray());
        }

        /// <summary>
        /// 逐订阅者触发帧事件；单个订阅者抛异常不影响其余订阅者与后续接收
        /// </summary>
        /// <param name="frame">已复制的完整帧</param>
        private void RaiseFrameReceived(byte[] frame)
        {
            var handler = FrameReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<byte[]>)subscribers[i])(frame);
                }
                catch (Exception ex)
                {
                    // 订阅者异常不能打断底层接收循环
                    P2PLogHelper.Debug("UdpTurnClientTransport", "帧回调异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 观察异步发送任务；只有失败才记日志，成功路径不产生额外开销
        /// </summary>
        /// <param name="sending">发送任务</param>
        private static void ObserveSendFailure(Task sending)
        {
            if (sending == null)
            {
                return;
            }

            if (sending.IsCompleted)
            {
                if (sending.IsFaulted)
                {
                    var ignored = sending.Exception;
                    P2PLogHelper.Debug("UdpTurnClientTransport", "发送失败：UDP 传输返回错误");
                }
                return;
            }

            sending.ContinueWith(
                t =>
                {
                    var ignored = t.Exception;
                    P2PLogHelper.Debug("UdpTurnClientTransport", "发送失败：UDP 传输返回错误");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
