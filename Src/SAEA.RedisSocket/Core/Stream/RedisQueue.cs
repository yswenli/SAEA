/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisQueue
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/7 17:44:38
*描述：
*=====================================================================
*修改时间：2021/1/7 17:44:38
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
        /// Subscribe,
        /// XREAD COUNT 2 STREAMS mystream writers 0 0,
        /// XREAD COUNT 2 STREAMS mystream writers 0-0 0-0,
        /// XREAD BLOCK 5000 COUNT 100 STREAMS mystream $
        /// </summary>
        /// <param name="topicIDs"></param>
        /// <param name="count"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
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
        /// CreateGroup,
        /// XGROUP CREATE mystream consumer-group-name $ MKSTREAM
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="group"></param>
        /// <param name="asc"></param>
        /// <returns></returns>
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
        /// Subscribe,
        /// XREADGROUP GROUP group consumer [COUNT count][BLOCK milliseconds] [NOACK] STREAMS key [key ...]ID [ID ...]
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="consumerName"></param>
        /// <param name="topicName"></param>
        /// <param name="redisId"></param>
        /// <param name="noAck"></param>
        /// <param name="count"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
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
        /// Commit,
        /// xack mystream groupA 1600590826794-2 1600590826794-3
        /// </summary>
        /// <param name="redisIDs"></param>
        /// <returns></returns>
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

        public void Dispose()
        {
            RedisConnection.Dispose();
        }
    }
}
