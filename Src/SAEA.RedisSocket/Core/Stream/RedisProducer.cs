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
*命名空间：SAEA.RedisSocket.Core.Stream
*文件名： RedisProducer
*版本号： v26.4.23.1
*唯一标识：c0561e9e-7002-4d73-9573-4312626330df
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：RedisProducer接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:23:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补齐英文与乱码注释的中文说明
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// Stream 生产者，按 XADD 命令发布消息
    /// </summary>
    public class RedisProducer
    {

        RedisQueue _redisQueue;

        /// <summary>
        /// 初始化 Stream 生产者
        /// </summary>
        /// <param name="redisConnection"></param>
        internal RedisProducer(RedisConnection redisConnection)
        {
            _redisQueue = new RedisQueue(redisConnection);
        }

        /// <summary>
        /// 发布数据
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="fields"></param>
        /// <param name="id"></param>
        /// <param name="maxLen"></param>
        /// <returns></returns>
        public RedisID Publish(string topic, IEnumerable<RedisField> fields, string id = "*", int maxLen = -1)
        {
            return _redisQueue.Publish(topic, fields, id, maxLen);
        }

        /// <summary>
        /// 发布数据
        /// </summary>
        /// <param name="topic"></param>        
        /// <param name="value"></param>
        /// <param name="filed"></param>
        /// <returns></returns>
        public RedisID Publish(string topic, string value, string filed = "saea.redissocket")
        {
            return _redisQueue.Publish(topic, new RedisField[] { new RedisField(filed, value) });
        }

        //
    }
}
