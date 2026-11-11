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
*文件名： TurnAllocationTable
*版本号： v26.11.5.1
*唯一标识：362df3ad-bc1a-43ae-9b68-95d2e16af170
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN 分配表
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 分配表
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 分配表：按「客户端来源端点 + 控制连接本地端点 + 中继传输」隔离，
    /// 同一客户端在两种传输上的分配互不覆盖。
    /// </summary>
    public sealed class TurnAllocationTable
    {
        /// <summary>
        /// 按隔离键索引的分配
        /// </summary>
        private readonly Dictionary<string, TurnAllocation> _allocations = new Dictionary<string, TurnAllocation>();

        /// <summary>
        /// 保护分配表的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 当前分配数量
        /// </summary>
        public int Count
        {
            get { lock (_sync) { return _allocations.Count; } }
        }

        /// <summary>
        /// 统计指定用户当前的分配数量
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>分配数量</returns>
        public int CountByUser(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return 0;
            }

            var count = 0;
            lock (_sync)
            {
                foreach (var pair in _allocations)
                {
                    if (string.Equals(pair.Value.Username, username, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 加入一个分配；同一隔离键已存在时返回 false
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <returns>加入成功返回 true</returns>
        /// <exception cref="ArgumentNullException">allocation 为 null</exception>
        public bool TryAdd(TurnAllocation allocation)
        {
            if (allocation == null)
            {
                throw new ArgumentNullException(nameof(allocation));
            }

            var key = BuildKey(allocation.ClientEndPoint, allocation.LocalEndPoint, allocation.Transport);
            lock (_sync)
            {
                if (_allocations.ContainsKey(key))
                {
                    return false;
                }

                _allocations.Add(key, allocation);
                return true;
            }
        }

        /// <summary>
        /// 按隔离键取分配
        /// </summary>
        /// <param name="clientEndPoint">客户端来源端点</param>
        /// <param name="localEndPoint">控制连接本地端点</param>
        /// <param name="transport">中继传输协议号</param>
        /// <param name="allocation">取到的分配；失败时为 null</param>
        /// <returns>存在返回 true</returns>
        public bool TryGet(IPEndPoint clientEndPoint, IPEndPoint localEndPoint, byte transport, out TurnAllocation allocation)
        {
            allocation = null;
            if (clientEndPoint == null || localEndPoint == null)
            {
                return false;
            }

            var key = BuildKey(clientEndPoint, localEndPoint, transport);
            lock (_sync)
            {
                return _allocations.TryGetValue(key, out allocation);
            }
        }

        /// <summary>
        /// 按「客户端来源端点 + 控制连接本地端点」取分配，不区分中继传输；
        /// Refresh 等不携带传输类型的请求用它定位唯一的分配。
        /// </summary>
        /// <param name="clientEndPoint">客户端来源端点</param>
        /// <param name="localEndPoint">控制连接本地端点</param>
        /// <param name="allocation">取到的分配；失败时为 null</param>
        /// <returns>存在返回 true</returns>
        public bool TryGetAny(IPEndPoint clientEndPoint, IPEndPoint localEndPoint, out TurnAllocation allocation)
        {
            allocation = null;
            if (clientEndPoint == null || localEndPoint == null)
            {
                return false;
            }

            var prefix = Normalize(clientEndPoint.Address).ToString() + "|" + clientEndPoint.Port
                + "|" + Normalize(localEndPoint.Address) + "|" + localEndPoint.Port + "|";
            lock (_sync)
            {
                foreach (var pair in _allocations)
                {
                    if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        allocation = pair.Value;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 移除并返回全部分配；调用方负责释放
        /// </summary>
        /// <returns>全部被移除的分配</returns>
        public IReadOnlyList<TurnAllocation> Clear()
        {
            var removed = new List<TurnAllocation>();
            lock (_sync)
            {
                foreach (var pair in _allocations)
                {
                    removed.Add(pair.Value);
                }
                _allocations.Clear();
            }
            return removed;
        }

        /// <summary>
        /// 按隔离键移除分配
        /// </summary>
        /// <param name="clientEndPoint">客户端来源端点</param>
        /// <param name="localEndPoint">控制连接本地端点</param>
        /// <param name="transport">中继传输协议号</param>
        /// <param name="allocation">被移除的分配；不存在时为 null</param>
        /// <returns>移除成功返回 true</returns>
        public bool TryRemove(IPEndPoint clientEndPoint, IPEndPoint localEndPoint, byte transport, out TurnAllocation allocation)
        {
            allocation = null;
            if (clientEndPoint == null || localEndPoint == null)
            {
                return false;
            }

            var key = BuildKey(clientEndPoint, localEndPoint, transport);
            lock (_sync)
            {
                if (!_allocations.TryGetValue(key, out allocation))
                {
                    return false;
                }

                _allocations.Remove(key);
                return true;
            }
        }

        /// <summary>
        /// 移除并返回全部过期分配；调用方负责释放
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <returns>被移除的分配</returns>
        public IReadOnlyList<TurnAllocation> RemoveExpired(DateTimeOffset now)
        {
            var removed = new List<TurnAllocation>();
            lock (_sync)
            {
                var keys = new List<string>();
                foreach (var pair in _allocations)
                {
                    if (pair.Value.IsExpired(now))
                    {
                        keys.Add(pair.Key);
                    }
                }

                for (var i = 0; i < keys.Count; i++)
                {
                    removed.Add(_allocations[keys[i]]);
                    _allocations.Remove(keys[i]);
                }
            }
            return removed;
        }

        /// <summary>
        /// 取全部分配的只读快照
        /// </summary>
        /// <returns>分配快照</returns>
        public IReadOnlyList<TurnAllocation> Snapshot()
        {
            lock (_sync)
            {
                var result = new List<TurnAllocation>(_allocations.Count);
                foreach (var pair in _allocations)
                {
                    result.Add(pair.Value);
                }
                return result;
            }
        }

        /// <summary>
        /// 生成隔离键；v4-mapped v6 地址先归一化为 v4
        /// </summary>
        /// <param name="clientEndPoint">客户端来源端点</param>
        /// <param name="localEndPoint">控制连接本地端点</param>
        /// <param name="transport">中继传输协议号</param>
        /// <returns>隔离键</returns>
        private static string BuildKey(IPEndPoint clientEndPoint, IPEndPoint localEndPoint, byte transport)
        {
            return Normalize(clientEndPoint.Address).ToString()
                + "|" + clientEndPoint.Port
                + "|" + Normalize(localEndPoint.Address)
                + "|" + localEndPoint.Port
                + "|" + transport;
        }

        /// <summary>
        /// 把 v4-mapped v6 地址归一化为 v4
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>归一化后的地址</returns>
        private static IPAddress Normalize(IPAddress address)
        {
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4();
            }
            return address;
        }
    }
}