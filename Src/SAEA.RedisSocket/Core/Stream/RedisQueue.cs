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
*文件名： RedisQueue
*版本号： v26.4.23.1
*唯一标识：37429457-26b9-4854-9965-4187576d4fc8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：RedisQueue接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// 创建redis 流队列,
    /// redis5.0
    /// </summary>
    public class RedisQueue : IDisposable
    {
        /// <summary>
        /// 连接包装类
        /// </summary>
        internal RedisConnection RedisConnection { get; private set; }

        /// <summary>
        /// 创建redis 流队列,
        /// redis5.0
        /// </summary>
        /// <param name="redisConnection"></param>
        internal RedisQueue(RedisConnection redisConnection)
        {
            RedisConnection = redisConnection;
        }

        /// <summary>
        /// 获取指定队列长度
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        public long GetLength(string topic)
        {
            if (long.TryParse(RedisConnection.DoWithKey(RequestType.XLEN, topic).Data, out long len))
            {
                return len;
            }
            return -1;
        }

        /// <summary>
        /// 返回范围内的消息信息,
        /// xrange streamname fromId toId count value
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<IdFiled> GetRange(string topic, string from, string to, int count = -1)
        {
            List<string> @params = new List<string>();
            @params.Add(topic);
            @params.Add(from);
            @params.Add(to);
            if (count > 0)
            {
                @params.Add("COUNT");
                @params.Add(count.ToString());
            }
            var responseData = RedisConnection.DoStreamRange(@params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                throw new Exception(responseData.Data);
            }
            return responseData.Entity;
        }
        /// <summary>
        /// 返回范围内的消息信息
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<IdFiled> GetRange(string topic, RedisID from, RedisID to, int count = -1)
        {
            return GetRange(topic, from.ToString(), to.ToString(), count);
        }
        /// <summary>
        /// 返回范围内的消息信息
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<IdFiled> GetRange(string topic, int count = -1)
        {
            return GetRange(topic, "-", "+", count);
        }
        /// <summary>
        /// 发布消息，
        /// XADD mystream MAXLEN ~ 1000 *
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="fields"></param>
        /// <param name="id"></param>
        /// <param name="maxLen"></param>
        /// <returns></returns>
        public RedisID Publish(string topic, IEnumerable<RedisField> fields, string id = "*", int maxLen = -1)
        {
            List<string> @params = new List<string>();
            @params.Add(topic);
            if (maxLen > 0)
            {
                @params.Add("MAXLEN");
                @params.Add("~");
                @params.Add(maxLen.ToString());
            }
            if (string.IsNullOrEmpty(id))
                @params.Add("*");
            else
                @params.Add(id);

            foreach (var item in fields)
            {
                @params.Add(item.Field);
                @params.Add(item.String);
            }

            var responseData = RedisConnection.DoWithMutiParams(RequestType.XADD, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                throw new Exception(responseData.Data);
            }
            return new RedisID(responseData.Data.Trim());
        }

        /// <summary>
        /// 读取一个或多个主题中指定位置之后的消息，blocked 为 true 时按 timeout 毫秒阻塞等待
        /// </summary>
        /// <param name="topicIDs">主题与读取起始 ID 的组合集合</param>
        /// <param name="count">单次最多读取的消息数量</param>
        /// <param name="blocked">是否阻塞等待新消息</param>
        /// <param name="timeout">阻塞超时毫秒数，仅 blocked 为 true 时生效</param>
        /// <returns>读取到的消息条目集合</returns>
        public IEnumerable<StreamEntry> Subscribe(IEnumerable<TopicID> topicIDs, int count = 1, bool blocked = false, int timeout = 1000)
        {
            List<string> @params = new List<string>();

            if (blocked)
            {
                @params.Add("BLOCK");
                @params.Add(timeout.ToString());
            }

            @params.Add("COUNT");
            @params.Add(count.ToString());

            @params.Add("STREAMS");

            var list = topicIDs.ToList();

            var topics = list.Select(b => b.Topic).ToList();
            var redisIDs = list.Select(b => b.RedisID).ToList();

            foreach (var topic in topics)
            {
                @params.Add(topic);
            }

            foreach (var redisID in redisIDs)
            {
                @params.Add(redisID);
            }

            var responseData = RedisConnection.DoStreamSub(RequestType.XREAD, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                throw new Exception(responseData.Data);
            }

            return responseData.Entity;
        }


        /// <summary>
        /// 创建消费组，asc 为 true 时从最早消息开始消费，否则从最新消息开始；组已存在时返回 true
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="group">消费组名</param>
        /// <param name="asc">是否从最早消息开始消费</param>
        /// <returns>创建成功或组已存在返回 true</returns>
        public bool CreateGroup(string topic, string group, bool asc = true)
        {
            List<string> @params = new List<string>();
            @params.Add("CREATE");
            @params.Add(topic);
            @params.Add(group);
            if (asc)
            {
                @params.Add("0");
            }
            else
            {
                @params.Add("$");
            }
            @params.Add("MKSTREAM");

            var responseData = RedisConnection.DoWithMutiParams(RequestType.XGROUP, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                if (responseData.Data.IndexOf("-BUSYGROUP", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    return true;
                }
                throw new Exception(responseData.Data);
            }
            return (responseData.Data.Trim() == "OK");
        }

        /// <summary>
        /// 删除组,
        /// XGROUP DESTROY mystream consumer-group-name
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public bool RemoveGroup(string topic, string group)
        {
            List<string> @params = new List<string>();
            @params.Add("DESTROY");
            @params.Add(topic);
            @params.Add(group);

            var responseData = RedisConnection.DoWithMutiParams(RequestType.XGROUP, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                if (responseData.Data.IndexOf("-BUSYGROUP", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    return true;
                }
                throw new Exception(responseData.Data);
            }
            return (responseData.Data.Trim() == "1");
        }

        /// <summary>
        /// 移除消费者组中的消费者,
        /// XGROUP DELCONSUMER mystream consumer-group-name myconsumer123
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="group"></param>
        /// <param name="consumerName"></param>
        /// <returns></returns>
        public bool RemoveConsumerName(string topic, string group, string consumerName)
        {
            List<string> @params = new List<string>();
            @params.Add("DELCONSUMER");
            @params.Add(topic);
            @params.Add(group);
            @params.Add(consumerName);

            var responseData = RedisConnection.DoWithMutiParams(RequestType.XGROUP, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                if (responseData.Data.IndexOf("-BUSYGROUP", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    return true;
                }
                throw new Exception(responseData.Data);
            }
            return (responseData.Data.Trim() == "OK");
        }

        /// <summary>
        /// 以消费组身份读取消息，redisId 为 null 时读取尚未投递的新消息，noAck 为 true 时不写入待确认列表
        /// </summary>
        /// <param name="groupName">消费组名</param>
        /// <param name="consumerName">消费者名</param>
        /// <param name="topicName">主题名</param>
        /// <param name="redisId">读取起始 ID，为 null 时按新消息读取</param>
        /// <param name="noAck">是否跳过待确认列表</param>
        /// <param name="count">单次最多读取的消息数量</param>
        /// <param name="blocked">是否阻塞等待新消息</param>
        /// <param name="timeout">阻塞超时毫秒数，仅 blocked 为 true 时生效</param>
        /// <returns>读取到的消息条目集合</returns>
        public IEnumerable<StreamEntry> Subscribe(string groupName, string consumerName, string topicName, RedisID redisId, bool noAck = false, int count = 1, bool blocked = false, int timeout = 1000)
        {
            List<string> @params = new List<string>();

            @params.Add("GROUP");
            @params.Add(groupName);
            @params.Add(consumerName);

            @params.Add("COUNT");
            @params.Add(count.ToString());

            if (blocked)
            {
                @params.Add("BLOCK");
                @params.Add(timeout.ToString());
            }

            if (noAck)
            {
                @params.Add("NOACK");
            }

            @params.Add("STREAMS");
            @params.Add(topicName);
            if (redisId != null)
                @params.Add(redisId.ToString());
            else
                @params.Add(">");

            var responseData = RedisConnection.DoStreamSub(RequestType.XREADGROUP, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                throw new Exception(responseData.Data);
            }

            return responseData.Entity;
        }

        /// <summary>
        /// 确认消费组中已处理的消息，ID 集合为空时直接返回 0
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="group">消费组名</param>
        /// <param name="redisIDs">待确认的消息 ID 集合</param>
        /// <returns>成功确认的消息数量</returns>
        public int Commit(string topic, string group, IEnumerable<RedisID> redisIDs)
        {
            if (redisIDs == null || !redisIDs.Any()) return 0;

            List<string> @params = new List<string>();
            @params.Add(topic);
            @params.Add(group);

            foreach (var item in redisIDs)
            {
                @params.Add(item.ToString());
            }
            var responseData = RedisConnection.DoWithMutiParams(RequestType.XACK, @params.ToArray());

            if (responseData == null) throw new Exception("Time out");

            if (responseData.Type == ResponseType.Error)
            {
                throw new Exception(responseData.Data);
            }
            return int.Parse(responseData.Data);
        }

        /// <summary>
        /// 释放连接包装类持有的连接
        /// </summary>
        public void Dispose()
        {
            RedisConnection.Dispose();
        }
    }
}
