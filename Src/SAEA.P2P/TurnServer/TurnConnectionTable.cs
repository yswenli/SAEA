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
*命名空间：SAEA.P2P.TurnServer
*文件名： TurnConnectionTable
*版本号： v26.11.5.1
*唯一标识：5db99ad2-cedd-49d8-857d-97b2ae664943
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 04:00:00
*描述：TURN TCP 中继连接表
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 05:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：连接表增加按分配清理过期连接、对端重复判断与整体清空
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// 一条 TCP 中继连接的登记项：连接 ID、所属分配、对端地址与 socket，以及是否已绑定为数据连接。
    /// </summary>
    internal sealed class TurnConnectionRecord
    {
        /// <summary>
        /// 连接 ID；由连接表分配，ConnectionBind 用它定位连接
        /// </summary>
        internal uint ConnectionId { get; set; }

        /// <summary>
        /// 所属分配
        /// </summary>
        internal TurnAllocation Allocation { get; set; }

        /// <summary>
        /// 对端地址，即中继监听到的连接来源
        /// </summary>
        internal IPEndPoint PeerEndPoint { get; set; }

        /// <summary>
        /// 中继监听接受的对端 socket
        /// </summary>
        internal Socket PeerSocket { get; set; }

        /// <summary>
        /// 是否来自 TLS 中继端口；ConnectionBind 必须与控制连接保持同一加密层
        /// </summary>
        internal bool IsTls { get; set; }

        /// <summary>
        /// 接受连接的时间
        /// </summary>
        internal DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// 是否已被 ConnectionBind 绑定为数据连接；未绑定的连接超时后直接关闭
        /// </summary>
        internal bool Bound { get; set; }
    }

    /// <summary>
    /// TURN TCP 中继连接表：按连接 ID 索引所有 TCP 中继连接，跨分配共用。
    /// 加锁只做集合操作，关闭 socket 与流一律交给调用方在锁外完成。
    /// </summary>
    internal sealed class TurnConnectionTable
    {
        /// <summary>
        /// 按连接 ID 索引的连接
        /// </summary>
        private readonly Dictionary<uint, TurnConnectionRecord> _connections = new Dictionary<uint, TurnConnectionRecord>();

        /// <summary>
        /// 保护连接表与 ID 计数器的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 最近分配出去的连接 ID；从 1 开始单调递增
        /// </summary>
        private uint _lastConnectionId;

        /// <summary>
        /// 当前连接数量
        /// </summary>
        internal int Count
        {
            get { lock (_sync) { return _connections.Count; } }
        }

        /// <summary>
        /// 登记一条连接并分配连接 ID
        /// </summary>
        /// <param name="record">连接登记项</param>
        /// <returns>分配到的连接 ID</returns>
        /// <exception cref="ArgumentNullException">record 为 null</exception>
        /// <exception cref="InvalidOperationException">连接 ID 已耗尽</exception>
        internal uint Add(TurnConnectionRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            // 取号与插入必须在同一把锁内，否则并发登记会撞上同一个 ID
            lock (_sync)
            {
                // ID 到顶后不回绕：回绕会让仍在用的旧连接和新连接共用一个 ID
                if (_lastConnectionId == uint.MaxValue)
                {
                    throw new InvalidOperationException("TCP 中继连接 ID 已耗尽");
                }

                _lastConnectionId++;
                record.ConnectionId = _lastConnectionId;
                _connections.Add(record.ConnectionId, record);
                return record.ConnectionId;
            }
        }

        /// <summary>
        /// 按连接 ID 取连接
        /// </summary>
        /// <param name="connectionId">连接 ID</param>
        /// <param name="record">取到的连接；失败时为 null</param>
        /// <returns>存在返回 true</returns>
        internal bool TryGet(uint connectionId, out TurnConnectionRecord record)
        {
            lock (_sync)
            {
                return _connections.TryGetValue(connectionId, out record);
            }
        }

        /// <summary>
        /// 按连接 ID 移除连接
        /// </summary>
        /// <param name="connectionId">连接 ID</param>
        /// <param name="record">被移除的连接；不存在时为 null</param>
        /// <returns>移除成功返回 true</returns>
        internal bool TryRemove(uint connectionId, out TurnConnectionRecord record)
        {
            lock (_sync)
            {
                if (!_connections.TryGetValue(connectionId, out record))
                {
                    return false;
                }

                _connections.Remove(connectionId);
                return true;
            }
        }

        /// <summary>
        /// 移除并返回超时未绑定的连接；调用方负责关闭这些连接的 socket
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <param name="bindTimeout">等待 ConnectionBind 的超时时间</param>
        /// <returns>被移除的连接</returns>
        internal List<TurnConnectionRecord> RemoveExpired(DateTimeOffset now, TimeSpan bindTimeout)
        {
            var removed = new List<TurnConnectionRecord>();
            lock (_sync)
            {
                List<uint> expired = null;
                foreach (var pair in _connections)
                {
                    var record = pair.Value;
                    if (record.Bound)
                    {
                        continue;
                    }

                    if (now - record.CreatedAt < bindTimeout)
                    {
                        continue;
                    }

                    if (expired == null)
                    {
                        expired = new List<uint>();
                    }
                    expired.Add(pair.Key);
                }

                if (expired != null)
                {
                    for (var i = 0; i < expired.Count; i++)
                    {
                        removed.Add(_connections[expired[i]]);
                        _connections.Remove(expired[i]);
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// 移除并返回某个分配名下超时未绑定的连接；调用方负责关闭这些连接的 socket
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="now">当前时间</param>
        /// <param name="bindTimeout">等待 ConnectionBind 的超时时间</param>
        /// <returns>被移除的连接</returns>
        internal List<TurnConnectionRecord> RemoveExpired(TurnAllocation allocation, DateTimeOffset now, TimeSpan bindTimeout)
        {
            var removed = new List<TurnConnectionRecord>();
            if (allocation == null)
            {
                return removed;
            }

            lock (_sync)
            {
                List<uint> expired = null;
                foreach (var pair in _connections)
                {
                    var record = pair.Value;

                    // 只清理本分配名下的记录：别的分配的连接由各自的 relay 负责摘除与关闭，
                    // 在这里顺手摘掉会让对方的中继端不再知道这条连接已被回收
                    if (!ReferenceEquals(record.Allocation, allocation))
                    {
                        continue;
                    }

                    if (record.Bound)
                    {
                        continue;
                    }

                    if (now - record.CreatedAt < bindTimeout)
                    {
                        continue;
                    }

                    if (expired == null)
                    {
                        expired = new List<uint>();
                    }
                    expired.Add(pair.Key);
                }

                if (expired != null)
                {
                    for (var i = 0; i < expired.Count; i++)
                    {
                        removed.Add(_connections[expired[i]]);
                        _connections.Remove(expired[i]);
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// 判断某个分配名下是否已存在指向同一对端的连接；未绑定与已绑定的记录都算
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="peerEndPoint">对端地址，IP 与端口都要比较</param>
        /// <returns>已存在返回 true</returns>
        internal bool ContainsPeer(TurnAllocation allocation, IPEndPoint peerEndPoint)
        {
            if (allocation == null || peerEndPoint == null)
            {
                return false;
            }

            lock (_sync)
            {
                foreach (var pair in _connections)
                {
                    var record = pair.Value;
                    if (!ReferenceEquals(record.Allocation, allocation))
                    {
                        continue;
                    }

                    var peer = record.PeerEndPoint;
                    if (peer == null)
                    {
                        continue;
                    }

                    if (peer.Port == peerEndPoint.Port && Normalize(peer.Address).Equals(Normalize(peerEndPoint.Address)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 移除并返回全部连接；服务端释放时兜底用，调用方负责关闭这些连接的 socket
        /// </summary>
        /// <returns>被移除的连接</returns>
        internal List<TurnConnectionRecord> RemoveAll()
        {
            lock (_sync)
            {
                var removed = new List<TurnConnectionRecord>(_connections.Values);
                _connections.Clear();
                return removed;
            }
        }

        /// <summary>
        /// 移除并返回某个分配名下的全部连接；调用方负责关闭这些连接的 socket 与流
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <returns>被移除的连接</returns>
        internal List<TurnConnectionRecord> RemoveByAllocation(TurnAllocation allocation)
        {
            var removed = new List<TurnConnectionRecord>();
            if (allocation == null)
            {
                return removed;
            }

            lock (_sync)
            {
                List<uint> matched = null;
                foreach (var pair in _connections)
                {
                    if (!ReferenceEquals(pair.Value.Allocation, allocation))
                    {
                        continue;
                    }

                    if (matched == null)
                    {
                        matched = new List<uint>();
                    }
                    matched.Add(pair.Key);
                }

                if (matched != null)
                {
                    for (var i = 0; i < matched.Count; i++)
                    {
                        removed.Add(_connections[matched[i]]);
                        _connections.Remove(matched[i]);
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// 把 v4-mapped v6 地址归一化为 v4，避免同一 IP 因形态不同被当成两个对端
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>归一化后的地址</returns>
        private static IPAddress Normalize(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4();
            }
            return address;
        }
    }
}