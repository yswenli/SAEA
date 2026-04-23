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
*唯一标识：6bded7f2-c389-4973-8bbb-1d9d1df99087
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/01/08 20:00:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// RedisProducer
    /// </summary>
    public class RedisProducer
    {

        RedisQueue _redisQueue;

        /// <summary>
        /// RedisProducer
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
