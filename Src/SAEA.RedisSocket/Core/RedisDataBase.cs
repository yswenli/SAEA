using SAEA.RedisSocket.Model;
using SAEA.RedisSocket.Net;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis数据库操作类
    /// </summary>
    public class RedisDataBase : RedisOperator
    {
        object _syncLocker = new object();

        RedisCoder _redisCoder;

        RedisLock _redisLock;

        public RedisDataBase(RedisConnection cnn) : base(cnn)
        {
            _redisCoder = cnn.RedisCoder;
            _redisLock = new RedisLock(cnn);
        }

        #region KEY
        public void Set(string key, string value)
        {
            base.Do(RequestType.SET, key, value);
        }

        public void Set(string key, string value, int seconds)
        {
            base.DoExpire(RequestType.SET, key, value, seconds);
        }

        public string Get(string key)
        {
            return base.Do(RequestType.GET, key).Data;
        }
        public ResponseData Keys(string pattern = "*")
        {
            return base.Do(RequestType.KEYS, pattern);
        }
        public void Del(string key)
        {
            base.Do(RequestType.DEL, key);
        }
        /// <summary>
        /// 检查给定 key 是否存在。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exists(string key)
        {
            var result = base.Do(RequestType.EXISTS, key).Type;
            return result == ResponseType.Empty ? false : true;
        }
        /// <summary>
        /// 为给定 key 设置生存时间，当 key 过期时(生存时间为 0 )，它会被自动删除。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        public void Expire(string key, int seconds)
        {
            base.DoExpire(key, seconds);
        }
        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="key"></param>
        public void Persist(string key)
        {
            base.Do(RequestType.PERSIST, key);
        }
        #endregion

        #region HSET
        public void HSet(string hid, string key, string value)
        {
            base.Do(RequestType.HSET, hid, key, value);
        }
        public ResponseData HGet(string hid, string key)
        {
            return base.Do(RequestType.HGET, hid, key);
        }
        public ResponseData HGetAll(string hid)
        {
            return base.Do(RequestType.HGETALL, hid);
        }
        public ResponseData GetHKeys(string hid)
        {
            return base.Do(RequestType.HKEYS, hid);
        }
        public ResponseData HDel(string hid, string key)
        {
            return base.Do(RequestType.HDEL, hid, key);
        }
        public ResponseData HDel(string hid, string[] keys)
        {
            return base.DoBatch(RequestType.HDEL, hid, keys);
        }
        public int HLen(string hid)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.HLEN, hid).Data, out result);
            return result;
        }
        public bool HExists(string hid, string key)
        {
            var result = base.Do(RequestType.HEXISTS, hid, key).Type;
            return result == ResponseType.Empty ? false : true;
        }
        #endregion

        #region List
        public void LSet(string key, int index, string value)
        {
            base.Do(RequestType.LSET, key, index.ToString(), value);
        }
        public int LLen(string key)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.LLEN, key).Data, out result);
            return result;
        }
        public int LPush(string key, string value)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.LPUSH, key, value).Data, out result);
            return result;
        }
        public string LPop(string key)
        {
            return base.Do(RequestType.LPOP, key).Data;
        }
        public int RPush(string key, string value)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.RPUSH, key, value).Data, out result);
            return result;
        }
        public string RPop(string key)
        {
            return base.Do(RequestType.RPOP, key).Data;
        }
        public ResponseData LRang(string key, int begin = 0, int end = -1)
        {
            return base.Do(RequestType.LRANGE, key, begin.ToString(), end.ToString());
        }
        public int LRemove(string key, int begin = 0, int end = -1)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.LREM, key, begin.ToString(), end.ToString()).Data, out result);
            return result;
        }
        #endregion

        #region SET
        public void SAdd(string key, string value)
        {
            base.Do(RequestType.SADD, key, value);
        }

        public void SAdd(string key, string[] value)
        {
            base.DoBatch(RequestType.SADD, key, value);
        }

        public int SLen(string key)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.SCARD, key).Data, out result);
            return result;
        }

        public bool SExists(string key)
        {
            var result = base.Do(RequestType.SISMEMBER, key).Type;
            return result == ResponseType.Empty ? false : true;
        }

        public ResponseData SMemebers(string key)
        {
            return base.Do(RequestType.SMEMBERS, key);
        }

        public string SRandMemeber(string key)
        {
            return base.Do(RequestType.SRANDMEMBER, key).Data;
        }

        public string SPop(string key)
        {
            return base.Do(RequestType.SPOP, key).Data;
        }
        public int SRemove(string key, string[] values)
        {
            var result = 0;
            int.TryParse(base.DoBatch(RequestType.SREM, key, values).Data, out result);
            return result;
        }
        #endregion

        #region ZSET
        public void ZAdd(string key, string value, double score)
        {
            base.Do(RequestType.ZADD, key, value, score.ToString());
        }
        public void ZAdd(string key, Dictionary<string, double> dic)
        {
            base.DoBatch(RequestType.ZADD, key, dic);
        }
        public int ZLen(string key)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.ZCARD, key).Data, out result);
            return result;
        }
        public int ZCount(string key, double begin = 0, double end = -1)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.ZCOUNT, key, begin.ToString(), end.ToString()).Data, out result);
            return result;
        }

        public ResponseData ZRang(string key, double begin = 0, double end = -1)
        {
            return base.Do(RequestType.ZRANGE, key, begin, end);
        }
        public ResponseData ZRevrange(string key, double begin = 0, double end = -1)
        {
            return base.Do(RequestType.ZREVRANGE, key, begin, end);
        }
        public int ZRemove(string key, string[] values)
        {
            var result = 0;
            int.TryParse(base.DoBatch(RequestType.ZREM, key, values).Data, out result);
            return result;
        }
        #endregion

        #region SCAN
        public ResponseData Scan(int offset = 0, string pattern = "*", int count = -1)
        {
            return base.Do(RequestType.SCAN, offset, pattern, count);
        }

        public ResponseData HScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return base.Do(RequestType.HSCAN, hid, offset, pattern, count);
        }

        public ResponseData SScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return base.Do(RequestType.SSCAN, hid, offset, pattern, count);
        }

        public ResponseData ZScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return base.Do(RequestType.ZSCAN, hid, offset, pattern, count);
        }
        #endregion

        #region Pub/Sub
        public int Publish(string channel, string value)
        {
            var result = 0;
            int.TryParse(base.Do(RequestType.PUBLISH, channel, value).Data, out result);
            return result;
        }

        public void Suscribe(Action<string, string> onMsg, params string[] channels)
        {
            base.DoSub(channels, onMsg);
        }

        public void UNSUBSCRIBE(string channel)
        {
            base.Do(RequestType.UNSUBSCRIBE, channel);
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
