using SAEA.RedisSocket.Base;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis数据库操作类
    /// </summary>
    public class RedisDataBase : BaseOperation
    {
        object _syncLocker = new object();

        RedisCoder _redisCoder;

        RedisLock _redisLock;

        public RedisDataBase(RedisConnection cnn) : base(cnn)
        {
            _redisCoder = cnn.RedisCoder;
            _redisLock = new RedisLock(cnn);
        }

        public void Init(RedisConnection cnn)
        {
            _redisCoder = cnn.RedisCoder;
            _redisLock = new RedisLock(cnn);
        }

        #region KEY
        public void Set(string key, string value)
        {
            base.DoWithKeyValue(RequestType.SET, key, value, true);
        }

        public void Set(string key, string value, int seconds)
        {
            base.DoExpireInsert(RequestType.SET, key, value, seconds);
        }

        public void MSet(Dictionary<string, string> dic)
        {
            base.DoBatchWithDic(RequestType.MSET, dic);
        }

        public string Get(string key)
        {
            return base.DoWithKey(RequestType.GET, key, true).Data;
        }

        public List<string> MGet(params string[] keys)
        {
            return base.DoBatchWithParams(RequestType.MGET, keys).ToList<string>();
        }

        public List<string> Keys(string pattern = "*")
        {
            return base.DoWithKey(RequestType.KEYS, pattern, true).ToList<string>();
        }
        public void Del(string key)
        {
            base.DoWithKey(RequestType.DEL, key, true);
        }

        public void Del(params string[] keys)
        {
            base.DoBatchWithParams(RequestType.DEL, keys);
        }
        /// <summary>
        /// 检查给定 key 是否存在。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exists(string key)
        {
            var result = base.DoWithKey(RequestType.EXISTS, key, true).Type;
            return result == ResponseType.Empty ? false : true;
        }
        /// <summary>
        /// 为给定 key 设置生存时间，当 key 过期时(生存时间为 0 )，它会被自动删除。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        public void Expire(string key, int seconds)
        {
            base.DoExpire(key, seconds, true);
        }
        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="key"></param>
        public void Persist(string key)
        {
            base.DoWithKey(RequestType.PERSIST, key, true);
        }
        /// <summary>
        /// 将 oldKey 改名为 newkey 。
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <returns></returns>
        public bool Rename(string oldKey, string newKey)
        {
            var result = base.DoWithKeyValue(RequestType.RENAME, oldKey, newKey, true);
            if (result.Data == "OK")
            {
                return true;
            }
            return false;
        }
        #endregion

        #region HSET
        public void HSet(string hid, string key, string value)
        {
            base.DoHash(RequestType.HSET, hid, key, value, true);
        }
        public ResponseData HGet(string hid, string key)
        {
            return base.DoWithKeyValue(RequestType.HGET, hid, key, true);
        }
        public Dictionary<string, string> HGetAll(string hid)
        {
            return base.DoWithKey(RequestType.HGETALL, hid, true).ToKeyValues();
        }
        public List<string> GetHKeys(string hid)
        {
            return base.DoWithKey(RequestType.HKEYS, hid, true).ToList<string>();
        }
        public ResponseData HDel(string hid, string key)
        {
            return base.DoWithKeyValue(RequestType.HDEL, hid, key, true);
        }
        public ResponseData HDel(string hid, string[] keys)
        {
            return base.DoBatchWithIDKeys(RequestType.HDEL, hid, keys);
        }
        public int HLen(string hid)
        {
            var result = 0;
            int.TryParse(base.DoWithKey(RequestType.HLEN, hid, true).Data, out result);
            return result;
        }
        public bool HExists(string hid, string key)
        {
            var result = base.DoWithKeyValue(RequestType.HEXISTS, hid, key, true).Type;
            return result == ResponseType.Empty ? false : true;
        }
        #endregion

        #region List
        public void LSet(string key, int index, string value)
        {
            base.DoHash(RequestType.LSET, key, index.ToString(), value, true);
        }
        public int LLen(string key)
        {
            var result = 0;
            int.TryParse(base.DoWithKey(RequestType.LLEN, key, true).Data, out result);
            return result;
        }
        public int LPush(string key, string value)
        {
            var result = 0;
            int.TryParse(base.DoWithKeyValue(RequestType.LPUSH, key, value, true).Data, out result);
            return result;
        }
        public string LPop(string key)
        {
            return base.DoWithKey(RequestType.LPOP, key, true).Data;
        }
        public int RPush(string key, string value)
        {
            var result = 0;
            int.TryParse(base.DoWithKeyValue(RequestType.RPUSH, key, value, true).Data, out result);
            return result;
        }
        public string RPop(string key)
        {
            return base.DoWithKey(RequestType.RPOP, key, true).Data;
        }
        public List<string> LRang(string key, int begin = 0, int end = -1)
        {
            return base.DoHash(RequestType.LRANGE, key, begin.ToString(), end.ToString(), true).ToList<string>();
        }
        public int LRemove(string key, int count, string value)
        {
            var result = 0;
            int.TryParse(base.DoHash(RequestType.LREM, key, count.ToString(), value, true).Data, out result);
            return result;
        }
        #endregion

        #region SET
        public void SAdd(string key, string value)
        {
            base.DoWithKeyValue(RequestType.SADD, key, value, true);
        }

        public void SAdd(string key, string[] value)
        {
            base.DoBatchWithIDKeys(RequestType.SADD, key, value);
        }

        public int SLen(string key)
        {
            var result = 0;
            int.TryParse(base.DoWithKey(RequestType.SCARD, key, true).Data, out result);
            return result;
        }

        public bool SExists(string key, string value)
        {
            var result = base.DoWithKeyValue(RequestType.SISMEMBER, key, value, true).Type;
            return result == ResponseType.Empty ? false : true;
        }

        public List<string> SMemebers(string key)
        {
            return base.DoWithKey(RequestType.SMEMBERS, key, true).ToList<string>();
        }

        public string SRandMemeber(string key)
        {
            return base.DoWithKey(RequestType.SRANDMEMBER, key, true).Data;
        }

        public string SPop(string key)
        {
            return base.DoWithKey(RequestType.SPOP, key, true).Data;
        }
        public int SRemove(string key, params string[] values)
        {
            var result = 0;
            int.TryParse(base.DoBatchWithIDKeys(RequestType.SREM, key, values).Data, out result);
            return result;
        }
        #endregion

        #region ZSET
        public void ZAdd(string key, string value, double score)
        {
            base.DoHash(RequestType.ZADD, key, score.ToString(), value, true);
        }

        public void ZAdd(string key, Dictionary<double, string> scoreVals)
        {
            base.DoBatchWithIDDic(RequestType.ZADD, key, scoreVals, true);
        }

        public int ZLen(string key)
        {
            var result = 0;
            int.TryParse(base.DoWithKey(RequestType.ZCARD, key, true).Data, out result);
            return result;
        }
        public int ZCount(string key, double begin = 0, double end = -1)
        {
            var result = 0;
            int.TryParse(base.DoHash(RequestType.ZCOUNT, key, begin.ToString(), end.ToString(), true).Data, out result);
            return result;
        }

        public List<ZItem> ZRang(string key, double begin = 0, double end = -1)
        {
            return base.DoRang(RequestType.ZRANGE, key, begin, end, true).ToList();
        }

        public List<ZItem> ZRevrange(string key, double begin = 0, double end = -1)
        {
            return base.DoRang(RequestType.ZREVRANGE, key, begin, end, true).ToList();
        }

        public int ZRemove(string key, string[] values)
        {
            var result = 0;
            int.TryParse(base.DoBatchWithIDKeys(RequestType.ZREM, key, values).Data, out result);
            return result;
        }
        #endregion

        #region SCAN
        public ScanResponse Scan(int offset = 0, string pattern = "*", int count = -1)
        {
            return base.DoScan(RequestType.SCAN, offset, pattern, count);
        }

        public HScanResponse HScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return base.DoScanKey(RequestType.HSCAN, hid, offset, pattern, count).ToHScanResponse();
        }

        public ScanResponse SScan(string sid, int offset = 0, string pattern = "*", int count = -1)
        {
            return base.DoScanKey(RequestType.SSCAN, sid, offset, pattern, count);
        }

        public ZScanResponse ZScan(string zid, int offset = 0, string pattern = "*", int count = -1)
        {
            return base.DoScanKey(RequestType.ZSCAN, zid, offset, pattern, count).ToZScanResponse();
        }
        #endregion

        #region Pub/Sub
        public int Publish(string channel, string value)
        {
            var result = 0;
            int.TryParse(base.DoWithKeyValue(RequestType.PUBLISH, channel, value, true).Data, out result);
            return result;
        }

        public void Suscribe(Action<string, string> onMsg, params string[] channels)
        {
            base.DoSub(channels, onMsg);
        }

        public void UNSUBSCRIBE(string channel)
        {
            base.DoWithKey(RequestType.UNSUBSCRIBE, channel, true);
        }
        #endregion

        #region LOCK
        public bool Lock(string key, int seconds)
        {
            return _redisLock.Lock(key, seconds);
        }
        public void Unlock(string key = "")
        {
            _redisLock.Unlock(key);
        }
        #endregion

    }
}
