/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnectionManager
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
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


        /// <summary>
        /// getkey
        /// </summary>
        /// <param name="ipPort"></param>
        /// <param name="isMaster"></param>
        /// <returns></returns>
        static string GetKey(string ipPort, bool isMaster)
        {
            return ipPort + (isMaster ? "_master" : "_slave");
        }



        /// <summary>
        /// 设置RedisConnection
        /// </summary>
        /// <param name="ipPort"></param>
        /// <param name="isMaster"></param>
        /// <param name="cnn"></param>
        public static void Set(string ipPort, bool isMaster, RedisConnection cnn)
        {
            _redisConnections.TryAdd(GetKey(ipPort, isMaster), cnn);
        }

        /// <summary>
        /// 获取RedisConnection
        /// </summary>
        /// <param name="ipPort"></param>
        /// <param name="isMaster"></param>
        /// <returns></returns>
        public static RedisConnection Get(string ipPort, bool isMaster = true)
        {
            if (_redisConnections.TryGetValue(GetKey(ipPort, isMaster), out RedisConnection redisConnection))
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
            var result = _redisConnections.ContainsKey(GetKey(ipPort, true));

            if (!result)
            {
                result = _redisConnections.ContainsKey(GetKey(ipPort, false));
            }
            return result;
        }

        /// <summary>
        /// 移除连接对象缓存
        /// </summary>
        /// <param name="ipPort"></param>
        public static void Remove(string ipPort)
        {
            _redisConnections.TryRemove(GetKey(ipPort, true), out RedisConnection redisConnection1);
            _redisConnections.TryRemove(GetKey(ipPort, false), out RedisConnection redisConnection2);
        }


        /// <summary>
        /// 清理全部内容
        /// </summary>
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
