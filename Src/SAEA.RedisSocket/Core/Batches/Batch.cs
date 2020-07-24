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
        RedisDataBase _redisDataBase;

        List<BatchItem> _batchData;

        RedisCoder _redisCode;

        /// <summary>
        /// Redis批量操作类
        /// </summary>
        /// <param name="redisDataBase"></param>
        internal Batch(RedisDataBase redisDataBase)
        {
            _redisDataBase = redisDataBase;

            _redisCode = redisDataBase.RedisConnection.RedisCoder;

            _batchData = new List<BatchItem>();
        }

        /// <summary>
        /// 执行批量操作
        /// </summary>
        /// <returns></returns>
        public List<object> Execute()
        {
            if (!_batchData.Any()) return null;

            List<object> result = new List<object>();

            StringBuilder sb = new StringBuilder();

            foreach (var item in _batchData)
            {
                sb.Append(item.Cmd);
            }

            _redisCode.Request(sb.ToString());

            foreach (var item in _batchData)
            {
                var data = _redisCode.Decoder(item.RequestType);

                switch (item.RequestType)
                {
                    case RequestType.AUTH:
                    case RequestType.FLUSHALL:
                    case RequestType.SELECT:
                    case RequestType.SLAVEOF:
                    case RequestType.SET:
                    case RequestType.MSET:
                    case RequestType.MSETNX:
                    case RequestType.DEL:
                    case RequestType.HSET:
                    case RequestType.HMSET:
                    case RequestType.LSET:
                    case RequestType.LTRIM:
                    case RequestType.RENAME:
                    case RequestType.CLUSTER_MEET:
                    case RequestType.CLUSTER_FORGET:
                    case RequestType.CLUSTER_REPLICATE:
                    case RequestType.CLUSTER_SAVECONFIG:
                    case RequestType.CLUSTER_ADDSLOTS:
                    case RequestType.CLUSTER_DELSLOTS:
                    case RequestType.CLUSTER_FLUSHSLOTS:
                    case RequestType.CLUSTER_SETSLOT:
                    case RequestType.CONFIG_SET:

                        break;
                    case RequestType.GET:
                    case RequestType.GETSET:
                    case RequestType.HGET:
                    case RequestType.LPOP:
                    case RequestType.RPOP:
                    case RequestType.SRANDMEMBER:
                    case RequestType.SPOP:
                    case RequestType.RANDOMKEY:

                        break;
                    case RequestType.KEYS:
                    case RequestType.MGET:
                    case RequestType.HKEYS:
                    case RequestType.HVALS:
                    case RequestType.HMGET:
                    case RequestType.LRANGE:
                    case RequestType.BLPOP:
                    case RequestType.BRPOP:
                    case RequestType.SMEMBERS:
                    case RequestType.SINTER:
                    case RequestType.SUNION:
                    case RequestType.SDIFF:
                    case RequestType.ZRANGEBYLEX:
                    case RequestType.CLUSTER_GETKEYSINSLOT:
                    case RequestType.CONFIG_GET:

                        break;
                    case RequestType.DBSIZE:
                    case RequestType.FLUSHDB:
                    case RequestType.STRLEN:
                    case RequestType.APPEND:
                    case RequestType.TTL:
                    case RequestType.PTTL:
                    case RequestType.EXISTS:
                    case RequestType.EXPIRE:
                    case RequestType.EXPIREAT:
                    case RequestType.PERSIST:
                    case RequestType.SETNX:
                    case RequestType.HEXISTS:
                    case RequestType.HLEN:
                    case RequestType.HDEL:
                    case RequestType.HSTRLEN:
                    case RequestType.HINCRBY:
                    case RequestType.LLEN:
                    case RequestType.INCR:
                    case RequestType.INCRBY:
                    case RequestType.DECR:
                    case RequestType.DECRBY:
                    case RequestType.LPUSH:
                    case RequestType.LPUSHX:
                    case RequestType.RPUSH:
                    case RequestType.RPUSHX:
                    case RequestType.RPOPLPUSH:
                    case RequestType.LINSERT:
                    case RequestType.SADD:
                    case RequestType.SCARD:
                    case RequestType.SISMEMBER:
                    case RequestType.SREM:
                    case RequestType.SMOVE:
                    case RequestType.SINTERSTORE:
                    case RequestType.SUNIONSTORE:
                    case RequestType.LREM:
                    case RequestType.SDIFFSTORE:
                    case RequestType.ZADD:
                    case RequestType.ZSCORE:
                    case RequestType.ZINCRBY:
                    case RequestType.ZCARD:
                    case RequestType.ZCOUNT:
                    case RequestType.ZRANK:
                    case RequestType.ZREVRANK:
                    case RequestType.ZREM:
                    case RequestType.ZREMRANGEBYRANK:
                    case RequestType.ZREMRANGEBYSCORE:
                    case RequestType.ZLEXCOUNT:
                    case RequestType.ZREMRANGEBYLEX:
                    case RequestType.PUBLISH:
                    case RequestType.CLUSTER_KEYSLOT:
                    case RequestType.CLUSTER_COUNTKEYSINSLOT:
                    case RequestType.GEOADD:

                        break;
                    case RequestType.GEOPOS:
                    case RequestType.GEORADIUS:
                    case RequestType.GEORADIUSBYMEMBER:

                        break;

                }
            }

            return result;
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

            _batchData.Add(new BatchItem(RequestType.DECR, cmd));
        }

        public void DelAsync(params string[] keys)
        {
            var cmd = _redisCode.Coder(RequestType.DECRBY, keys);

            _batchData.Add(new BatchItem(RequestType.DECR, cmd));
        }



        public void ExistsAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.EXISTS, key);

            _batchData.Add(new BatchItem(RequestType.DECR, cmd));
        }

        public void ExpireAsync(string key, int seconds)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIRE, key, seconds.ToString());

            _batchData.Add(new BatchItem(RequestType.DECR, cmd));
        }

        public void ExpireAtAsync(string key, DateTime dateTime)
        {
            throw new NotImplementedException();
        }

        public void ExpireAtAsync(string key, int timestamp)
        {
            throw new NotImplementedException();
        }

        public void GeoAddAsync(string key, params GeoItem[] items)
        {
            throw new NotImplementedException();
        }

        public void GeoDistAsync(string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m)
        {
            throw new NotImplementedException();
        }

        public void GetAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void GetSetAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void HDelAsync(string hid, string key)
        {
            throw new NotImplementedException();
        }

        public void HDelAsync(string hid, string[] keys)
        {
            throw new NotImplementedException();
        }

        public void HExistsAsync(string hid, string key)
        {
            throw new NotImplementedException();
        }

        public void HGetAsync(string hid, string key)
        {
            throw new NotImplementedException();
        }

        public void HIncrementByAsync(string hid, string key, int num)
        {
            throw new NotImplementedException();
        }

        public void HIncrementByFloatAsync(string hid, string key, float num)
        {
            throw new NotImplementedException();
        }

        public void HLenAsync(string hid)
        {
            throw new NotImplementedException();
        }

        public void HMSetAsync(string hid, Dictionary<string, string> keyvalues)
        {
            throw new NotImplementedException();
        }

        public void HSetAsync(string hid, string key, string value)
        {
            throw new NotImplementedException();
        }

        public void HStrLenAsync(string hid, string key)
        {
            throw new NotImplementedException();
        }

        public void IncrementAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void IncrementByAsync(string key, int num)
        {
            throw new NotImplementedException();
        }

        public void IncrementByFloatAsync(string key, float num)
        {
            throw new NotImplementedException();
        }

        public void LenAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void LIndexAsync(string key, int index)
        {
            throw new NotImplementedException();
        }

        public void LInsertAsync(string key, string pivot, bool isBefore, string value)
        {
            throw new NotImplementedException();
        }

        public void LLenAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void LPopAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void LPushAsync(string key, List<string> lists)
        {
            throw new NotImplementedException();
        }

        public void LPushAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void LPushXAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void LRemoveAsync(string key, int count, string value)
        {
            throw new NotImplementedException();
        }

        public void LSetAsync(string key, int index, string value)
        {
            throw new NotImplementedException();
        }

        public void LTrimAsync(string key, int begin = 0, int end = -1)
        {
            throw new NotImplementedException();
        }

        public void MSetAsync(Dictionary<string, string> dic)
        {
            throw new NotImplementedException();
        }

        public void MSetNxAsync(Dictionary<string, string> dic)
        {
            throw new NotImplementedException();
        }

        public void PersistAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void PttlAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void RandomKeyAsync(TimeSpan timeSpan)
        {
            throw new NotImplementedException();
        }

        public void RenameAsync(string oldKey, string newKey)
        {
            throw new NotImplementedException();
        }

        public void RPopAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void RpopLPushAsync(string source, string destination)
        {
            throw new NotImplementedException();
        }

        public void RPushAsync(string key, List<string> values)
        {
            throw new NotImplementedException();
        }

        public void RPushAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void RPushXAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SAddAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SAddAsync(string key, string[] value)
        {
            throw new NotImplementedException();
        }

        public void SDiffStoreAsync(string destination, params string[] keys)
        {
            throw new NotImplementedException();
        }

        public void SetAsync(string key, string value, int seconds)
        {
            throw new NotImplementedException();
        }

        public void SetAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SExistsAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SInterStoreAsync(string destination, params string[] keys)
        {
            throw new NotImplementedException();
        }

        public void SLenAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void SPopAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void SRandMemeberAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void SRemoveAsync(string key, params string[] values)
        {
            throw new NotImplementedException();
        }

        public void SUnionStoreAsync(string destination, params string[] keys)
        {
            throw new NotImplementedException();
        }

        public void TtlAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void ZAddAsync(string key, Dictionary<double, string> scoreVals)
        {
            throw new NotImplementedException();
        }

        public void ZAddAsync(string key, string value, double score)
        {
            throw new NotImplementedException();
        }

        public void ZCountAsync(string key, double begin = -2147483648, double end = 2147483647)
        {
            throw new NotImplementedException();
        }

        public void ZIncrByAsync(string key, double increment, string value)
        {
            throw new NotImplementedException();
        }

        public void ZIncrByAsync(string key, long increment, string value)
        {
            throw new NotImplementedException();
        }

        public void ZLenAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void ZLexCountAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            throw new NotImplementedException();
        }

        public void ZRankAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void ZRemoveAsync(string key, string[] values)
        {
            throw new NotImplementedException();
        }

        public void ZRemoveByLexAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            throw new NotImplementedException();
        }

        public void ZRemoveByRankAsync(string key, double start = 0, double stop = -1)
        {
            throw new NotImplementedException();
        }

        public void ZRemoveByScoreAsync(string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None)
        {
            throw new NotImplementedException();
        }

        public void ZRevRankAsync(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void ZScoreAsync(string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}
