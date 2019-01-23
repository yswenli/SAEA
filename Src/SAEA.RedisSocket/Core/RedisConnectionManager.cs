/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnectionManager
*版本号： V4.0.0.1
*唯一标识：3d4f939c-3fb9-40e9-a0e0-c7ec773539ae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/22 10:37:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/22 10:37:15
*修改人： yswenli
*版本号： V4.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Base;
using SAEA.RedisSocket.Interface;
using SAEA.RedisSocket.Model;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis connection manager
    /// </summary>
    static class RedisConnectionManager
    {
        /// <summary>
        /// redis连接缓存
        /// </summary>
        static ConcurrentDictionary<string, RedisConnection> _redisConnections = new ConcurrentDictionary<string, RedisConnection>();


        public static List<ClusterNode> _clusterMasterNodes = new List<ClusterNode>();


        static object _locker = new object();

        /// <summary>
        /// 设置主节点信息缓存
        /// </summary>
        /// <param name="nodes"></param>
        public static void SetClusterNodes(List<ClusterNode> nodes)
        {
            lock (_locker)
            {
                var ids = nodes.Select(b => b.NodeID).ToList();
                _clusterMasterNodes.RemoveAll(b => ids.Contains(b.NodeID));
                _clusterMasterNodes.AddRange(nodes);
            }
        }

        /// <summary>
        /// 移除节点信息缓存
        /// </summary>
        /// <param name="ipPort"></param>
        public static void RemoveClusterNode(string ipPort)
        {
            lock (_locker)
            {
                _clusterMasterNodes.RemoveAll(b => b.IPPort == ipPort);
            }
        }

        public static string GetIPPortWidthKey(string key)
        {
            var slot = RedisCRC16.GetClusterSlot(key);
            string ipPort = string.Empty;
            lock (_locker)
            {
                return _clusterMasterNodes.Where(b => b.MinSlots <= slot && b.MaxSlots >= slot).Select(b => b.IPPort).First();
            }
        }

        /// <summary>
        /// 根据key获取对应连接对象
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static RedisConnection GetConnectionBySlot(string key)
        {
            return Get(GetIPPortWidthKey(key));
        }


        /// <summary>
        /// 设置RedisConnection
        /// </summary>
        /// <param name="ipPort"></param>
        /// <param name="cnn"></param>
        public static void Set(string ipPort, RedisConnection cnn)
        {
            _redisConnections.TryAdd(ipPort, cnn);
        }

        /// <summary>
        /// 获取RedisConnection
        /// </summary>
        /// <param name="ipPort"></param>
        /// <returns></returns>
        public static RedisConnection Get(string ipPort)
        {
            if (_redisConnections.TryGetValue(ipPort, out RedisConnection redisConnection))
            {
                return redisConnection;
            }
            return null;
        }

        /// <summary>
        /// 是否存在
        /// </summary>
        /// <param name="ipPort"></param>
        /// <returns></returns>
        public static bool Exsits(string ipPort)
        {
            return _redisConnections.ContainsKey(ipPort);
        }

        /// <summary>
        /// 移除连接对象缓存
        /// </summary>
        /// <param name="ipPort"></param>
        public static void Remove(string ipPort)
        {
            _redisConnections.TryRemove(ipPort, out RedisConnection redisConnection);
        }


        public static void Clear()
        {
            var list = _redisConnections.Values;
            foreach (var item in list)
            {
                item.Dispose();
            }
            _redisConnections.Clear();
        }
    }
}
