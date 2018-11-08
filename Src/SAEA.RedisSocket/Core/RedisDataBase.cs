using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis数据库操作类
    /// </summary>
    public class RedisDataBase
    {
        object _syncLocker = new object();

        RedisConnection _cnn;

        RedisLock _redisLock;

        public RedisDataBase(RedisConnection cnn)
        {
            _cnn = cnn;
            _redisLock = new RedisLock(_cnn);
        }

        #region KEY
        public void Set(string key, string value)
        {
            _cnn.DoWithKeyValue(RequestType.SET, key, value);
        }

        public void Set(string key, string value, int seconds)
        {
            _cnn.DoExpireInsert(RequestType.SET, key, value, seconds);
        }

        public void MSet(Dictionary<string, string> dic)
        {
            _cnn.DoBatchWithDic(RequestType.MSET, dic);
        }

        public string Get(string key)
        {
            return _cnn.DoWithKey(RequestType.GET, key).Data;
        }

        public List<string> MGet(params string[] keys)
        {
            return _cnn.DoBatchWithParams(RequestType.MGET, keys).ToList<string>();
        }

        public List<string> Keys(string pattern = "*")
        {
            return _cnn.DoWithKey(RequestType.KEYS, pattern).ToList<string>();
        }
        public void Del(string key)
        {
            _cnn.DoWithKey(RequestType.DEL, key);
        }

        public void Del(params string[] keys)
        {
            _cnn.DoBatchWithParams(RequestType.DEL, keys);
        }
        /// <summary>
        /// 检查给定 key 是否存在。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exists(string key)
        {
            var result = _cnn.DoWithKey(RequestType.EXISTS, key).Type;
            return result == ResponseType.Empty ? false : true;
        }
        /// <summary>
        /// 为给定 key 设置生存时间，当 key 过期时(生存时间为 0 )，它会被自动删除。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        public void Expire(string key, int seconds)
        {
            _cnn.DoExpire(key, seconds);
        }
        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="key"></param>
        public void Persist(string key)
        {
            _cnn.DoWithKey(RequestType.PERSIST, key);
        }
        /// <summary>
        /// 将 oldKey 改名为 newkey 。
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <returns></returns>
        public bool Rename(string oldKey, string newKey)
        {
            var result = _cnn.DoWithKeyValue(RequestType.RENAME, oldKey, newKey);
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
            _cnn.DoHash(RequestType.HSET, hid, key, value);
        }
        public ResponseData HGet(string hid, string key)
        {
            return _cnn.DoWithKeyValue(RequestType.HGET, hid, key);
        }
        public Dictionary<string, string> HGetAll(string hid)
        {
            return _cnn.DoWithKey(RequestType.HGETALL, hid).ToKeyValues();
        }
        public List<string> GetHKeys(string hid)
        {
            return _cnn.DoWithKey(RequestType.HKEYS, hid).ToList<string>();
        }
        public ResponseData HDel(string hid, string key)
        {
            return _cnn.DoWithKeyValue(RequestType.HDEL, hid, key);
        }
        public ResponseData HDel(string hid, string[] keys)
        {
            return _cnn.DoBatchWithIDKeys(RequestType.HDEL, hid, keys);
        }
        public int HLen(string hid)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKey(RequestType.HLEN, hid).Data, out result);
            return result;
        }
        public bool HExists(string hid, string key)
        {
            var result = _cnn.DoWithKeyValue(RequestType.HEXISTS, hid, key).Type;
            return result == ResponseType.Empty ? false : true;
        }
        #endregion

        #region List
        public void LSet(string key, int index, string value)
        {
            _cnn.DoHash(RequestType.LSET, key, index.ToString(), value);
        }
        public int LLen(string key)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKey(RequestType.LLEN, key).Data, out result);
            return result;
        }
        public int LPush(string key, string value)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKeyValue(RequestType.LPUSH, key, value).Data, out result);
            return result;
        }
        public string LPop(string key)
        {
            return _cnn.DoWithKey(RequestType.LPOP, key).Data;
        }
        public int RPush(string key, string value)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKeyValue(RequestType.RPUSH, key, value).Data, out result);
            return result;
        }
        public string RPop(string key)
        {
            return _cnn.DoWithKey(RequestType.RPOP, key).Data;
        }
        public List<string> LRang(string key, int begin = 0, int end = -1)
        {
            return _cnn.DoHash(RequestType.LRANGE, key, begin.ToString(), end.ToString()).ToList<string>();
        }
        public int LRemove(string key, int count, string value)
        {
            var result = 0;
            int.TryParse(_cnn.DoHash(RequestType.LREM, key, count.ToString(), value).Data, out result);
            return result;
        }
        #endregion

        #region SET
        public void SAdd(string key, string value)
        {
            _cnn.DoWithKeyValue(RequestType.SADD, key, value);
        }

        public void SAdd(string key, string[] value)
        {
            _cnn.DoBatchWithIDKeys(RequestType.SADD, key, value);
        }

        public int SLen(string key)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKey(RequestType.SCARD, key).Data, out result);
            return result;
        }

        public bool SExists(string key, string value)
        {
            var result = _cnn.DoWithKeyValue(RequestType.SISMEMBER, key, value).Type;
            return result == ResponseType.Empty ? false : true;
        }

        public List<string> SMemebers(string key)
        {
            return _cnn.DoWithKey(RequestType.SMEMBERS, key).ToList<string>();
        }

        public string SRandMemeber(string key)
        {
            return _cnn.DoWithKey(RequestType.SRANDMEMBER, key).Data;
        }

        public string SPop(string key)
        {
            return _cnn.DoWithKey(RequestType.SPOP, key).Data;
        }
        public int SRemove(string key, params string[] values)
        {
            var result = 0;
            int.TryParse(_cnn.DoBatchWithIDKeys(RequestType.SREM, key, values).Data, out result);
            return result;
        }
        #endregion

        #region ZSET
        public void ZAdd(string key, string value, double score)
        {
            _cnn.DoHash(RequestType.ZADD, key, score.ToString(), value);
        }

        public void ZAdd(string key, Dictionary<double, string> scoreVals)
        {
            _cnn.DoBatchWithIDDic(RequestType.ZADD, key, scoreVals);
        }

        public int ZLen(string key)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKey(RequestType.ZCARD, key).Data, out result);
            return result;
        }
        public int ZCount(string key, double begin = 0, double end = -1)
        {
            var result = 0;
            int.TryParse(_cnn.DoHash(RequestType.ZCOUNT, key, begin.ToString(), end.ToString()).Data, out result);
            return result;
        }

        public List<ZItem> ZRang(string key, double begin = 0, double end = -1)
        {
            return _cnn.DoRang(RequestType.ZRANGE, key, begin, end).ToList();
        }

        public List<ZItem> ZRevrange(string key, double begin = 0, double end = -1)
        {
            return _cnn.DoRang(RequestType.ZREVRANGE, key, begin, end).ToList();
        }

        public int ZRemove(string key, string[] values)
        {
            var result = 0;
            int.TryParse(_cnn.DoBatchWithIDKeys(RequestType.ZREM, key, values).Data, out result);
            return result;
        }
        #endregion

        #region SCAN
        public ScanResponse Scan(int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScan(RequestType.SCAN, offset, pattern, count);
        }

        public HScanResponse HScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScanKey(RequestType.HSCAN, hid, offset, pattern, count).ToHScanResponse();
        }

        public ScanResponse SScan(string sid, int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScanKey(RequestType.SSCAN, sid, offset, pattern, count);
        }

        public ZScanResponse ZScan(string zid, int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScanKey(RequestType.ZSCAN, zid, offset, pattern, count).ToZScanResponse();
        }
        #endregion

        #region Pub/Sub
        public int Publish(string channel, string value)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKeyValue(RequestType.PUBLISH, channel, value).Data, out result);
            return result;
        }

        public void Suscribe(Action<string, string> onMsg, params string[] channels)
        {
            _cnn.DoSub(channels, onMsg);
        }

        public void UNSUBSCRIBE(string channel)
        {
            _cnn.DoWithKey(RequestType.UNSUBSCRIBE, channel);
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
