/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisProducer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/8 17:38:12
*描述：
*=====================================================================
*修改时间：2021/1/8 17:38:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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

        //
    }
}
