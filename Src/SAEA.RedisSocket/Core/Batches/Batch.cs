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

            _batchData.Add(new BatchItem(RequestType.DECRBY, cmd));
        }

        public void DelAsync(params string[] keys)
        {
            var cmd = _redisCode.Coder(RequestType.DECRBY, keys);

            _batchData.Add(new BatchItem(RequestType.DECRBY, cmd));
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

        public void HDelAsync(string hid, string[] keys)
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
