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
*文件名： TurnClientOptions
*版本号： v26.11.5.1
*唯一标识：210535ed-f294-4842-bd79-993a8239b77a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 03:10:00
*描述：TURN 客户端配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 05:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增刷新请求超时 RefreshTimeout
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 客户端配置：服务端端点、传输工厂、两种凭据入口、三层刷新间隔、刷新请求超时与重连退避。
    /// 三个刷新间隔、刷新请求超时与两个退避间隔在赋值时校验必须大于 0，测试可调到 100ms 量级但不得为 0。
    /// </summary>
    public sealed class TurnClientOptions
    {
        /// <summary>
        /// allocation 刷新间隔
        /// </summary>
        private TimeSpan _allocationRefreshInterval = TimeSpan.FromSeconds(500);

        /// <summary>
        /// permission 刷新间隔
        /// </summary>
        private TimeSpan _permissionRefreshInterval = TimeSpan.FromSeconds(240);

        /// <summary>
        /// channel 刷新间隔
        /// </summary>
        private TimeSpan _channelRefreshInterval = TimeSpan.FromSeconds(480);

        /// <summary>
        /// 首次重连退避
        /// </summary>
        private TimeSpan _reconnectBackoffInitial = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 重连退避上限
        /// </summary>
        private TimeSpan _reconnectBackoffMax = TimeSpan.FromSeconds(8);

        /// <summary>
        /// 刷新类请求的等待上限
        /// </summary>
        private TimeSpan _refreshTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 定时器工厂；默认基于 <see cref="Timer"/> 的一次性定时器
        /// </summary>
        private Func<TimeSpan, Action, IDisposable> _timerFactory;

        /// <summary>
        /// 创建配置并安装默认定时器工厂
        /// </summary>
        public TurnClientOptions()
        {
            _timerFactory = CreateDefaultTimer;
        }

        /// <summary>
        /// TURN 服务端端点；必填
        /// </summary>
        public IPEndPoint ServerEndPoint { get; set; }

        /// <summary>
        /// 客户端传输工厂；必填，断线重连时重复调用，旧实例由 <see cref="TurnClient"/> 释放
        /// </summary>
        public Func<ITurnClientTransport> TransportFactory { get; set; }

        /// <summary>
        /// 直接配置的用户名，用于与 coturn 等入口互通；与 <see cref="Credential"/> 只能二选一
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 直接配置的口令，用于与 coturn 等入口互通；不得写入日志
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 信令下发的临时凭据；与 <see cref="Username"/> / <see cref="Password"/> 只能二选一
        /// </summary>
        public TurnCredential Credential { get; set; }

        /// <summary>
        /// STUN 事务初始重传间隔，单位毫秒
        /// </summary>
        public int RtoMilliseconds { get; set; } = StunTransactionTiming.RtoMilliseconds;

        /// <summary>
        /// allocation 刷新间隔，默认 500s
        /// </summary>
        public TimeSpan AllocationRefreshInterval
        {
            get { return _allocationRefreshInterval; }
            set { _allocationRefreshInterval = ValidateInterval(value, nameof(AllocationRefreshInterval)); }
        }

        /// <summary>
        /// permission 刷新间隔，默认 240s
        /// </summary>
        public TimeSpan PermissionRefreshInterval
        {
            get { return _permissionRefreshInterval; }
            set { _permissionRefreshInterval = ValidateInterval(value, nameof(PermissionRefreshInterval)); }
        }

        /// <summary>
        /// channel 刷新间隔，默认 480s
        /// </summary>
        public TimeSpan ChannelRefreshInterval
        {
            get { return _channelRefreshInterval; }
            set { _channelRefreshInterval = ValidateInterval(value, nameof(ChannelRefreshInterval)); }
        }

        /// <summary>
        /// 刷新类请求的等待上限，默认 10s；用于 allocation、permission、channel 刷新与重连后的 Allocate。
        /// 事务层默认要等满重传时刻表才会超时，这里给刷新路径一个更短的失败判定，避免死服务端把重连拖到几十秒。
        /// </summary>
        public TimeSpan RefreshTimeout
        {
            get { return _refreshTimeout; }
            set { _refreshTimeout = ValidateInterval(value, nameof(RefreshTimeout)); }
        }

        /// <summary>
        /// 首次重连退避，默认 500ms
        /// </summary>
        public TimeSpan ReconnectBackoffInitial
        {
            get { return _reconnectBackoffInitial; }
            set { _reconnectBackoffInitial = ValidateInterval(value, nameof(ReconnectBackoffInitial)); }
        }

        /// <summary>
        /// 重连退避上限，默认 8s
        /// </summary>
        public TimeSpan ReconnectBackoffMax
        {
            get { return _reconnectBackoffMax; }
            set { _reconnectBackoffMax = ValidateInterval(value, nameof(ReconnectBackoffMax)); }
        }

        /// <summary>
        /// 定时器工厂：按间隔创建一个一次性定时器，返回的实例在释放时停止该定时器；测试可注入手动触发器
        /// </summary>
        public Func<TimeSpan, Action, IDisposable> TimerFactory
        {
            get { return _timerFactory; }
            set { _timerFactory = value ?? CreateDefaultTimer; }
        }

        /// <summary>
        /// SOFTWARE 属性值；为 null 时不携带
        /// </summary>
        public string Software { get; set; }

        /// <summary>
        /// 校验必填项与组合关系；由 <see cref="TurnClient"/> 构造时调用
        /// </summary>
        /// <exception cref="InvalidOperationException">缺少服务端端点或传输工厂</exception>
        /// <exception cref="ArgumentOutOfRangeException">重传间隔不是正数</exception>
        /// <exception cref="ArgumentException">退避上限小于初始值</exception>
        internal void Validate()
        {
            if (ServerEndPoint == null)
            {
                throw new InvalidOperationException("必须配置 TURN 服务端端点 ServerEndPoint");
            }
            if (TransportFactory == null)
            {
                throw new InvalidOperationException("必须配置客户端传输工厂 TransportFactory");
            }
            if (RtoMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(RtoMilliseconds), "重传间隔必须大于 0");
            }
            if (ReconnectBackoffMax < ReconnectBackoffInitial)
            {
                throw new ArgumentException("重连退避上限不能小于初始值", nameof(ReconnectBackoffMax));
            }
        }

        /// <summary>
        /// 校验间隔必须大于 0，避免零间隔定时器变成忙循环
        /// </summary>
        /// <param name="value">间隔</param>
        /// <param name="name">属性名</param>
        /// <returns>原值</returns>
        /// <exception cref="ArgumentOutOfRangeException">间隔不是正数</exception>
        private static TimeSpan ValidateInterval(TimeSpan value, string name)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(name, "刷新与退避间隔必须大于 0");
            }
            return value;
        }

        /// <summary>
        /// 默认定时器：只触发一次，触发后自行释放，避免一次性定时器长期占用句柄
        /// </summary>
        /// <param name="interval">触发间隔</param>
        /// <param name="action">触发动作</param>
        /// <returns>可释放的定时器句柄</returns>
        private static IDisposable CreateDefaultTimer(TimeSpan interval, Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Timer timer = null;
            timer = new Timer(
                state =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        P2PLogHelper.Error("TurnClientOptions", "TURN_TIMER_FAILED", "TURN 定时器回调失败", ex);
                    }
                    finally
                    {
                        try
                        {
                            if (timer != null)
                            {
                                timer.Dispose();
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // 释放已被提前调用时忽略
                        }
                    }
                },
                null,
                interval,
                Timeout.InfiniteTimeSpan);

            return timer;
        }
    }
}