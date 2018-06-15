using SAEA.Common;
using SAEA.RedisSocket.Model;
using SAEA.RedisSocket.Net;
using System;

namespace SAEA.RedisSocket.Core
{
    class RedisLock : RedisOperator
    {
        RedisConnection _cnn;
        RedisCoder _redisCoder;
        object _syncLocker = new object();
        string _prefix = "redis_lock_";
        string _oldKey = string.Empty;

        public RedisLock(RedisConnection cnn) : base(cnn)
        {
            _cnn = cnn;
            _redisCoder = cnn.RedisCoder;
        }

        private DateTime GetDateTime()
        {
            DateTime dt = new DateTime();
            DateTime.TryParse(base.Do(RequestType.GET, _oldKey).Data, out dt);
            return dt;
        }

        private DateTime GetSetDateTime(string value)
        {
            DateTime dt = new DateTime();
            DateTime.TryParse(GetSet(_oldKey, value), out dt);
            return dt;
        }

        /// <summary>
        /// 设置指定 key 的值，并返回 key 旧的值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>返回给定 key 的旧值。 当 key 没有旧值时，即 key 不存在时，返回 nil 。当 key 存在但不是字符串类型时，返回一个错误。</returns>
        public string GetSet(string key, string value)
        {
            return base.Do(RequestType.GETSET, key, value).Data;
        }



        /// <summary>
        /// 命令在指定的 key 不存在时，为 key 设置指定的值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>若存在，返回false,否则返回true</returns>
        public bool SetNX(string key, string value)
        {
            if (base.Do(RequestType.SETNX, key, value).Data == "1")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 启用分布式锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        /// <returns>false 表示锁过期，true表示锁成功，阻塞过程表示正在等待锁</returns>
        public bool Lock(string key, int seconds)
        {
            lock (_syncLocker)
            {
                bool result = true;
                string ckey = _oldKey = string.Format("{0}{1}", _prefix, key);
                string expiredStr = DateTimeHelper.Now.AddSeconds(seconds).ToFString();
                while (!SetNX(ckey, expiredStr))
                {
                    if (GetDateTime() < DateTimeHelper.Now && GetSetDateTime(expiredStr) < DateTimeHelper.Now)
                    {
                        result = false;
                        break;
                    }
                    else
                    {
                        ThreadHelper.Sleep(10);
                    }
                }
                return result;
            }
        }
        /// <summary>
        /// 移除锁
        /// </summary>
        /// <param name="key"></param>
        public void Unlock(string key = "")
        {
            if (string.IsNullOrEmpty(key))
            {
                key = _oldKey;
            }
            base.Do(RequestType.DEL, key);
        }
    }
}
