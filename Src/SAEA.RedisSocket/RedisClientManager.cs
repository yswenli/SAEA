/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RedisManagerPool
*版本号： V3.2.1.1
*唯一标识：3d4f939c-3fb9-40e9-a0e0-c7ec773539ae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/19 10:37:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/19 10:37:15
*修改人： yswenli
*版本号： V3.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SAEA.RedisSocket
{
    /// <summary>
    /// 主从自适应Redis客户端管理类
    /// </summary>
    public class RedisClientManager
    {

        Dictionary<string, ConcurrentQueue<RedisClient>> _connections;

        object _syncLocker;

        /// <summary>
        /// 初始化Redis连接池
        /// </summary>
        /// <param name="ipAddress">例如：127.0.0.1:6379,192.168.1.3:6380</param>
        /// <param name="connections">每实例tcp连接数</param>
        /// <param name="password">redis密码</param>
        public RedisClientManager(string ipAddress, int connections = 2, string password = "")
        {
            lock (_syncLocker)
            {
                _syncLocker = new object();

                if (_connections == null)
                {
                    _connections = new Dictionary<string, ConcurrentQueue<RedisClient>>();

                    var arr = ipAddress.ToArray(false, ",");

                    if (arr != null)
                    {
                        foreach (var ipPortStr in arr)
                        {
                            var ipPort = ipPortStr.GetIPPort();

                            var queue = new ConcurrentQueue<RedisClient>();

                            try
                            {
                                bool isMaster = true;
                                for (int i = 0; i < connections; i++)
                                {
                                    var client = new RedisClient(ipPortStr, password);
                                    client.Connect();
                                    isMaster = client.IsMaster;
                                    queue.Enqueue(client);
                                }
                                if (isMaster)
                                    _connections.Add(RedisConst.Master, queue);
                                else
                                    _connections.Add(RedisConst.Slave, queue);
                            }
                            catch (Exception ex)
                            {
                                _connections.Clear();
                                throw new Exception("连接到" + ipPortStr + "失败！:" + ex.Message);
                            }
                        }
                    }
                }
            }

        }

        /// <summary>
        /// 获取redis连接对象
        /// </summary>
        /// <returns></returns>
        public PooledRedisClient GetRedisClient()
        {
            lock (_syncLocker)
            {
                PooledRedisClient pc = null;

                RedisClient client;

                if (_connections.ContainsKey(RedisConst.Master))
                {
                    while (!_connections[RedisConst.Master].TryDequeue(out client))
                    {
                        ThreadHelper.Sleep(1);
                    }
                    if (!client.IsConnected)
                    {
                        client.Connect();
                    }
                    pc = new PooledRedisClient();
                    pc.SetClient(this, client);
                }
                return pc;
            }
        }

        /// <summary>
        /// 获取redis连接从对象
        /// </summary>
        /// <returns></returns>
        public PooledRedisClient GetRedisClientReadOnly()
        {
            lock (_syncLocker)
            {
                PooledRedisClient pc = null;

                RedisClient client;

                if (_connections.ContainsKey(RedisConst.Slave))
                {
                    while (!_connections[RedisConst.Slave].TryDequeue(out client))
                    {
                        ThreadHelper.Sleep(1);
                    }
                    if (!client.IsConnected)
                    {
                        client.Connect();
                    }
                    pc = new PooledRedisClient();
                    pc.SetClient(this, client);
                }
                return pc;
            }
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        /// <param name="isMaster"></param>
        /// <param name="client"></param>
        internal void Free(bool isMaster, RedisClient client)
        {
            if (isMaster)
            {
                _connections[RedisConst.Master].Enqueue(client);
            }
            else
            {
                _connections[RedisConst.Slave].Enqueue(client);
            }
        }




    }
}
