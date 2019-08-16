/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisKeyOperation
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/14 14:52:51
*描述：
*=====================================================================
*修改时间：2019/8/14 14:52:51
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using SAEA.Common;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// keys操作
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 删除给定的一个或多个 key
        /// </summary>
        /// <param name="key"></param>
        public void Del(string key)
        {
            _cnn.DoWithKey(RequestType.DEL, key);
        }
        /// <summary>
        /// 删除给定的一个或多个 key
        /// </summary>
        /// <param name="keys"></param>
        public void Del(params string[] keys)
        {
            _cnn.DoWithMutiParams(RequestType.DEL, keys);
        }

        /// <summary>
        /// 检查给定 key 是否存在。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exists(string key)
        {
            var result = _cnn.DoWithKey(RequestType.EXISTS, key).Data;
            return result.IndexOf("0") > -1 ? false : true;
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
        /// 为给定 key 设置生存时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timestamp"></param>
        public void ExpireAt(string key, int timestamp)
        {
            _cnn.DoExpireAt(key, timestamp);
        }

        /// <summary>
        /// 为给定 key 设置生存时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dateTime"></param>
        public void ExpireAt(string key, DateTime dateTime)
        {
            ExpireAt(key, dateTime.ToUnixTick());
        }

        /// <summary>
        /// 查找所有符合给定模式 pattern 的 key 
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public List<string> Keys(string pattern = "*")
        {
            return _cnn.DoWithKey(RequestType.KEYS, pattern).ToList();
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
        /// 以秒为单位，返回给定 key 的剩余生存时间(TTL, time to live)
        /// </summary>
        /// <param name="key"></param>
        /// <returns>当 key 不存在时，返回 -2 。当 key 存在但没有设置剩余生存时间时，返回 -1 。否则，以秒为单位，返回 key 的剩余生存时间。 </returns>
        public int Ttl(string key)
        {
            int.TryParse(_cnn.DoWithKey(RequestType.TTL, key).Data, out int result);

            return result;

        }

        /// <summary>
        /// 以毫秒为单位，返回给定 key 的剩余生存时间(TTL, time to live)
        /// </summary>
        /// <param name="key"></param>
        /// <returns>当 key 不存在时，返回 -2 。当 key 存在但没有设置剩余生存时间时，返回 -1 。否则，以秒为单位，返回 key 的剩余生存时间。</returns>
        public long Pttl(string key)
        {
            long.TryParse(_cnn.DoWithKey(RequestType.PTTL, key).Data, out long result);

            return result;
        }

        /// <summary>
        /// 从当前数据库中随机返回(不删除)一个 key
        /// </summary>
        /// <returns></returns>
        public string RandomKey()
        {
            return _cnn.Do(RequestType.RANDOMKEY).Data;
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
    }
}
