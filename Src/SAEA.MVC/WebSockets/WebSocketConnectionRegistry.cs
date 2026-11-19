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
*命名空间：SAEA.MVC.WebSockets
*文件名： WebSocketConnectionRegistry
*版本号： v26.11.16.1
*唯一标识：721fc37a-77f7-47d1-92b3-0d24c74e6988
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:18:04
*描述：WebSocket 已升级连接表的登记与清扫
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 14:18:04
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增，承接 WebSocketsFeature 的连接表与清扫计时器
*
*****************************************************************************/
using SAEA.Http.Model;

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.MVC.WebSockets
{
    /// <summary>
    /// 已升级的 WebSocket 连接表：登记、摘除与残项清扫
    /// </summary>
    internal static class WebSocketConnectionRegistry
    {
        static readonly ConcurrentDictionary<string, MVCWebSocketConnection> _connections =
            new ConcurrentDictionary<string, MVCWebSocketConnection>();

        static Timer _sweeper;

        internal static void Register(MVCWebSocketConnection connection)
        {
            if (connection == null || string.IsNullOrEmpty(connection.Id)) return;

            _connections[connection.Id] = connection;

            EnsureSweeper();

            // 断开可能发生在发布之前（ConnectionClosed 早于 Register 到达），
            // 此时 TryRemove 会落空；注册后补一次检查，避免死连接一直留在表里
            if (!connection.IsAlive && _connections.TryRemove(connection.Id, out var dead))
            {
                dead.HandleDisconnected();
            }
        }

        internal static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            _connections.TryRemove(id, out _);
        }

        /// <summary>
        /// 底层断开通知：只有表里认识这个 sessionId 才处理，未知 id 直接忽略
        /// </summary>
        internal static void OnDisconnected(string id, Exception exception)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (_connections.TryRemove(id, out var connection))
            {
                connection.HandleDisconnected();
            }
        }

        /// <summary>
        /// 应用停止时中止全部连接
        /// </summary>
        internal static void StopAll()
        {
            foreach (var item in _connections)
            {
                item.Value.Abort();
            }

            _connections.Clear();

            // 宿主停止后释放扫描计时器并置空，下一次 Register 才能重建，避免遗留 5 分钟空转
            _sweeper?.Dispose();

            _sweeper = null;
        }

        internal static void EnsureSweeper()
        {
            if (_sweeper != null) return;

            var timer = new Timer(_ => Sweep(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            if (Interlocked.CompareExchange(ref _sweeper, timer, null) != null)
            {
                timer.Dispose();
            }
        }

        static void Sweep()
        {
            // State == Closed 覆盖正常收尾；IsAlive == false 覆盖错过断开通知的残项
            // （例如连接在登记前就已经断开，通知落到空表上）
            foreach (var item in _connections)
            {
                var connection = item.Value;

                if (connection.State != WebSocketState.Closed && connection.IsAlive) continue;

                if (_connections.TryRemove(item.Key, out var removed))
                {
                    // 幂等：已关闭的连接直接 no-op，仍在 Open 的死连接补一次收尾
                    removed.HandleDisconnected();
                }
            }
        }
    }
}