/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Batches
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Batches
*类 名 称：Batch
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/23 17:27:12
*描述：
*=====================================================================
*修改时间：2020/7/23 17:27:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RedisSocket.Core.Batches
{
    /// <summary>
    /// Redis批量操作类
    /// </summary>
    public class Batch : IBatch
    {
        List<BatchItem> _batchData;

        RedisCoder _redisCode;

        readonly object SyncRoot;

        /// <summary>
        /// Redis批量操作类
        /// </summary>
        /// <param name="redisDataBase"></param>
        internal Batch(RedisDataBase redisDataBase)
        {
            _redisCode = redisDataBase.RedisConnection.RedisCoder;

            SyncRoot = redisDataBase.RedisConnection.SyncRoot;

            _batchData = new List<BatchItem>();
        }
        /// <summary>
        /// 执行批量操作
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public List<string> Execute(int timeout = 10 * 1000)
        {
            return TaskHelper.Run((token) =>
            {
                List<string> result = null;

                lock (SyncRoot)
                {
                    if (!_batchData.Any()) return null;

                    result = new List<string>();

                    StringBuilder sb = new StringBuilder();

                    foreach (var item in _batchData)
                    {
                        sb.Append(item.Cmd);
                    }

                    _redisCode.Request(sb.ToString());

                    foreach (var item in _batchData)
                    {
                        var data = _redisCode.Decoder<string>(item.RequestType, token);

                        result.Add(data.Data);
                    }
                }
                return result;

            }, timeout).GetAwaiter().GetResult();

        }

        public void AppendAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.APPEND, key, value);

            _batchData.Add(new BatchItem(RequestType.APPEND, cmd));
        }


        public void DecrementAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.DECR, key);

            _batchData.Add(new BatchItem(RequestType.DECR, cmd));
        }

        public void DecrementByAsync(string key, int num)
        {
            var cmd = _redisCode.Coder(RequestType.DECRBY, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.DECRBY, cmd));
        }

        public void DelAsync(params string[] keys)
        {
            var cmd = _redisCode.Coder(RequestType.DEL, keys);

            _batchData.Add(new BatchItem(RequestType.DEL, cmd));
        }

        public void ExistsAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.EXISTS, key);

            _batchData.Add(new BatchItem(RequestType.EXISTS, cmd));
        }

        public void ExpireAsync(string key, int seconds)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIRE, key, seconds.ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIRE, cmd));
        }

        public void ExpireAtAsync(string key, DateTime dateTime)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIREAT, key, dateTime.ToUnixTick().ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIREAT, cmd));
        }

        public void ExpireAtAsync(string key, int timestamp)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIREAT, key, timestamp.ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIREAT, cmd));
        }

        public void GeoAddAsync(string key, params GeoItem[] items)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(GeoItem.ToParams(items));

            var cmd = _redisCode.Coder(RequestType.GEOADD, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.GEOADD, cmd));
        }

        public void GeoDistAsync(string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m)
        {
            var cmd = _redisCode.Coder(RequestType.GEODIST, key, member1, member2, geoUnit.ToString());

            _batchData.Add(new BatchItem(RequestType.GEODIST, cmd));
        }

        public void GetAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.GET, key);

            _batchData.Add(new BatchItem(RequestType.GET, cmd));
        }

        public void GetSetAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.GETSET, key, value);

            _batchData.Add(new BatchItem(RequestType.GETSET, cmd));
        }

        public void HDelAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HDEL, hid, key);

            _batchData.Add(new BatchItem(RequestType.HDEL, cmd));
        }

        public void HDelAsync(string hid, params string[] keys)
        {
            var list = new List<string>();

            list.Add(hid);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.HDEL, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.HDEL, cmd));
        }

        public void HExistsAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HEXISTS, hid, key);

            _batchData.Add(new BatchItem(RequestType.HEXISTS, cmd));
        }

        public void HGetAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HGET, hid, key);

            _batchData.Add(new BatchItem(RequestType.HGET, cmd));
        }

        public void HIncrementByAsync(string hid, string key, int num)
        {
            var cmd = _redisCode.Coder(RequestType.HINCRBY, hid, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.HINCRBY, cmd));
        }

        public void HIncrementByFloatAsync(string hid, string key, float num)
        {
            var cmd = _redisCode.Coder(RequestType.HINCRBYFLOAT, hid, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.HINCRBYFLOAT, cmd));
        }

        public void HLenAsync(string hid)
        {
            var cmd = _redisCode.Coder(RequestType.HLEN, hid);

            _batchData.Add(new BatchItem(RequestType.HLEN, cmd));
        }

        public void HSetAsync(string hid, string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.HSET, hid, key, value);

            _batchData.Add(new BatchItem(RequestType.HSET, cmd));
        }

        public void HStrLenAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HSTRLEN, hid, key);

            _batchData.Add(new BatchItem(RequestType.HSTRLEN, cmd));
        }

        public void IncrementAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.INCR, key);

            _batchData.Add(new BatchItem(RequestType.INCR, cmd));
        }

        public void IncrementByAsync(string key, int num)
        {
            var cmd = _redisCode.Coder(RequestType.INCRBY, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.INCRBY, cmd));
        }

        public void IncrementByFloatAsync(string key, float num)
        {
            var cmd = _redisCode.Coder(RequestType.INCRBY, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.INCRBY, cmd));
        }

        public void LenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.STRLEN, key);

            _batchData.Add(new BatchItem(RequestType.STRLEN, cmd));
        }

        public void LIndexAsync(string key, int index)
        {
            var cmd = _redisCode.Coder(RequestType.LINDEX, key, index.ToString());

            _batchData.Add(new BatchItem(RequestType.LINDEX, cmd));
        }

        public void LInsertAsync(string key, string pivot, bool isBefore, string value)
        {
            var beforStr = isBefore ? "BEFORE" : "AFTER";
            var list = new List<string>();
            list.Add(key);
            list.Add(beforStr);
            list.Add(pivot);
            list.Add(value);

            var cmd = _redisCode.Coder(RequestType.LINSERT, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.LINSERT, cmd));
        }

        public void LLenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.LLEN, key);

            _batchData.Add(new BatchItem(RequestType.LLEN, cmd));
        }

        public void LPopAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.LPOP, key);

            _batchData.Add(new BatchItem(RequestType.LPOP, cmd));
        }

        public void LPushAsync(string key, List<string> lists)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(lists);

            var cmd = _redisCode.Coder(RequestType.LPUSH, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.LPUSH, cmd));
        }

        public void LPushAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LPUSH, key, value);

            _batchData.Add(new BatchItem(RequestType.LPUSH, cmd));
        }

        public void LPushXAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LPUSHX, key, value);

            _batchData.Add(new BatchItem(RequestType.LPUSHX, cmd));
        }

        public void LRemoveAsync(string key, int count, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LREM, key, count.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.LREM, cmd));
        }

        public void LSetAsync(string key, int index, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LSET, key, index.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.LSET, cmd));
        }

        public void LTrimAsync(string key, int begin = 0, int end = -1)
        {
            var cmd = _redisCode.Coder(RequestType.LTRIM, key, begin.ToString(), end.ToString());

            _batchData.Add(new BatchItem(RequestType.LTRIM, cmd));
        }

        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="key"></param>
        public void PersistAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.PERSIST, key);

            _batchData.Add(new BatchItem(RequestType.PERSIST, cmd));
        }

        /// <summary>
        /// 这个命令类似于 TTL 命令，但它以毫秒为单位返回 key 的剩余生存时间，而不是像 TTL 命令那样，以秒为单位。
        /// </summary>
        /// <param name="key"></param>
        public void PttlAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.PTTL, key);

            _batchData.Add(new BatchItem(RequestType.PTTL, cmd));
        }

        /// <summary>
        /// 从当前数据库中随机返回(不删除)一个 key 。
        /// </summary>
        public void RandomKeyAsync()
        {
            var cmd = _redisCode.CodeOnlyParams(RequestType.RANDOMKEY);

            _batchData.Add(new BatchItem(RequestType.RANDOMKEY, cmd));
        }

        /// <summary>
        /// 将 key 改名为 newkey 。
        /// 当 key 和 newkey 相同，或者 key 不存在时，返回一个错误。
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        public void RenameAsync(string oldKey, string newKey)
        {
            var cmd = _redisCode.Coder(RequestType.RENAME, oldKey, newKey);

            _batchData.Add(new BatchItem(RequestType.RENAME, cmd));
        }

        /// <summary>
        /// 移除并返回列表 key 的尾元素
        /// </summary>
        /// <param name="key"></param>
        public void RPopAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.RPOP, key);

            _batchData.Add(new BatchItem(RequestType.RPOP, cmd));
        }

        public void RpopLPushAsync(string source, string destination)
        {
            var cmd = _redisCode.Coder(RequestType.RPOPLPUSH, source, destination);

            _batchData.Add(new BatchItem(RequestType.RPOPLPUSH, cmd));
        }

        /// <summary>
        /// 将一个或多个值 value 插入到列表 key 的表尾(最右边)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void RPushAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.RPUSH, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.RPUSH, cmd));
        }

        /// <summary>
        /// 将值 value 插入到列表 key 的表尾，当且仅当 key 存在并且是一个列表。        
        /// 和 RPUSH 命令相反，当 key 不存在时， RPUSHX 命令什么也不做。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void RPushXAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.RPUSHX, key, value);

            _batchData.Add(new BatchItem(RequestType.RPUSHX, cmd));
        }

        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void SAddAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.SADD, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SADD, cmd));
        }

        /// <summary>
        /// 这个命令的作用和 SDIFF 类似，但它将结果保存到 destination 集合，而不是简单地返回结果集。        
        /// 如果 destination 集合已经存在，则将其覆盖。
        /// destination 可以是 key 本身。
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        public void SDiffStoreAsync(string destination, params string[] keys)
        {
            var list = new List<string>();

            list.Add(destination);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.SDIFFSTORE, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SDIFFSTORE, cmd));
        }

        public void SetAsync(string key, string value, int seconds)
        {
            var cmd = _redisCode.Coder(RequestType.SET, key, value);

            _batchData.Add(new BatchItem(RequestType.SET, cmd));

            cmd = _redisCode.Coder(RequestType.EXPIRE, key, seconds.ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIRE, cmd));
        }

        public void SetAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.SET, key, value);

            _batchData.Add(new BatchItem(RequestType.SET, cmd));
        }

        /// <summary>
        /// 判断 member 元素是否集合 key 的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SExistsAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.SISMEMBER, key, value);

            _batchData.Add(new BatchItem(RequestType.SISMEMBER, cmd));
        }

        /// <summary>
        /// 返回集合交集数量并保存到 destination 集合
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        public void SInterStoreAsync(string destination, params string[] keys)
        {
            var list = new List<string>();

            list.Add(destination);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.SINTERSTORE, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SINTERSTORE, cmd));
        }

        /// <summary>
        /// 返回集合 key 的基数(集合中元素的数量)。
        /// </summary>
        /// <param name="key"></param>
        public void SLenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.SCARD, key);

            _batchData.Add(new BatchItem(RequestType.SCARD, cmd));
        }

        public void SPopAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.SPOP, key);

            _batchData.Add(new BatchItem(RequestType.SPOP, cmd));
        }

        public void SRandMemeberAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.SRANDMEMBER, key);

            _batchData.Add(new BatchItem(RequestType.SRANDMEMBER, cmd));
        }

        public void SRemoveAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.SREM, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SREM, cmd));
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的并集。并将集合保存到destination中
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        public void SUnionStoreAsync(string destination, params string[] keys)
        {
            var list = new List<string>();

            list.Add(destination);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.SUNIONSTORE, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SUNIONSTORE, cmd));
        }

        /// <summary>
        /// 返回生命周期
        /// </summary>
        /// <param name="key"></param>
        public void TtlAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.TTL, key);

            _batchData.Add(new BatchItem(RequestType.TTL, cmd));
        }

        public void ZAddAsync(string key, Dictionary<double, string> scoreVals)
        {
            var cmd = _redisCode.CodeForDicWidthID(RequestType.ZADD, key, scoreVals);

            _batchData.Add(new BatchItem(RequestType.ZADD, cmd));

        }

        public void ZAddAsync(string key, string value, double score)
        {
            var cmd = _redisCode.Coder(RequestType.ZADD, key, score.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.ZADD, cmd));

        }

        public void ZCountAsync(string key, double begin = -2147483648, double end = 2147483647)
        {
            var cmd = _redisCode.Coder(RequestType.ZCOUNT, key, begin.ToString(), end.ToString());

            _batchData.Add(new BatchItem(RequestType.ZCOUNT, cmd));
        }

        public void ZIncrByAsync(string key, double increment, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZINCRBY, key, increment.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.ZINCRBY, cmd));
        }

        public void ZIncrByAsync(string key, long increment, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZINCRBY, key, increment.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.ZINCRBY, cmd));
        }

        public void ZLenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.ZCARD, key);

            _batchData.Add(new BatchItem(RequestType.ZCARD, cmd));
        }

        public void ZLexCountAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            var cmd = _redisCode.Coder(RequestType.ZLEXCOUNT, key, min.ToString(), max.ToString(), offset.ToString(), count.ToString());

            _batchData.Add(new BatchItem(RequestType.ZLEXCOUNT, cmd));
        }

        /// <summary>
        /// 返回有序集 key 中成员 member 的排名
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ZRankAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZRANK, key, value);

            _batchData.Add(new BatchItem(RequestType.ZRANK, cmd));
        }
        /// <summary>
        /// 移除有序集 key 中的一个或多个成员，不存在的成员将被忽略。
        /// 当 key 存在但不是有序集类型时，返回一个错误
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void ZRemoveAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.ZREM, list.ToString());

            _batchData.Add(new BatchItem(RequestType.ZREM, cmd));
        }

        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void ZRemoveByLexAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            var cmd = _redisCode.CodeForRandByScore(RequestType.ZREMRANGEBYLEX, key, min, max, RangType.None, offset, count);

            _batchData.Add(new BatchItem(RequestType.ZREMRANGEBYLEX, cmd));
        }

        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void ZRemoveByRankAsync(string key, double start = 0, double stop = -1)
        {
            var cmd = _redisCode.CodeForRandByScore(RequestType.ZREMRANGEBYRANK, key, start, stop, RangType.None, -1, 20);

            _batchData.Add(new BatchItem(RequestType.ZREMRANGEBYRANK, cmd));
        }

        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        public void ZRemoveByScoreAsync(string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None)
        {
            var cmd = _redisCode.CodeForRandByScore(RequestType.ZREMRANGEBYSCORE, key, min, max, RangType.None, -1, 20);

            _batchData.Add(new BatchItem(RequestType.ZREMRANGEBYSCORE, cmd));
        }

        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。成员按 score 值递减
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ZRevRankAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZREVRANGE, key, value);

            _batchData.Add(new BatchItem(RequestType.ZREVRANGE, cmd));
        }

        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ZScoreAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZSCORE, key, value);

            _batchData.Add(new BatchItem(RequestType.ZSCORE, cmd));
        }
    }
}
