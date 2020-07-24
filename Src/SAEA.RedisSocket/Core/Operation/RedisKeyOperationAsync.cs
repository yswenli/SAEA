/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisKeyOperationAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/17 14:11:36
*描述：
*=====================================================================
*修改时间：2020/6/17 14:11:36
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// RedisKeyOperationAsync
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 删除给定的一个或多个 key
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        public async void DelAsync(TimeSpan timeSpan, string key)
        {
            await RedisConnection.DoWithKeyAsync(RequestType.DEL, key, timeSpan);
        }

        /// <summary>
        /// 删除给定的一个或多个 key
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        public async void DelAsync(TimeSpan timeSpan, params string[] keys)
        {
            await RedisConnection.DoWithMutiParamsAsync(RequestType.DEL, timeSpan, keys);
        }

        /// <summary>
        /// 检查给定 key 是否存在。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(TimeSpan timeSpan, string key)
        {
            var data = await RedisConnection.DoWithKeyAsync(RequestType.EXISTS, key, timeSpan);
            return data.Data.IndexOf("0") > -1 ? false : true;
        }

        /// <summary>
        /// 为给定 key 设置生存时间，当 key 过期时(生存时间为 0 )，它会被自动删除。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        public async void ExpireAsync(string key, int seconds)
        {
            await RedisConnection.DoExpireAsync(key, seconds);
        }

        /// <summary>
        /// 为给定 key 设置生存时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timestamp"></param>
        public async void ExpireAtAsync(string key, int timestamp)
        {
            await RedisConnection.DoExpireAtAsync(key, timestamp);
        }

        /// <summary>
        /// 为给定 key 设置生存时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dateTime"></param>
        public void ExpireAtAsync(string key, DateTime dateTime)
        {
            ExpireAtAsync(key, dateTime.ToUnixTick());
        }

        /// <summary>
        /// 查找所有符合给定模式 pattern 的 key 
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public async Task<List<string>> KeysAsync(TimeSpan timeSpan, string pattern = "*")
        {
            var data = await RedisConnection.DoWithKeyAsync(RequestType.KEYS, pattern, timeSpan);
            return data.ToList();
        }

        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        public async void PersistAsync(TimeSpan timeSpan, string key)
        {
            await RedisConnection.DoWithKeyAsync(RequestType.PERSIST, key, timeSpan);
        }

        /// <summary>
        /// 以秒为单位，返回给定 key 的剩余生存时间(TTL, time to live)
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns>当 key 不存在时，返回 -2 。当 key 存在但没有设置剩余生存时间时，返回 -1 。否则，以秒为单位，返回 key 的剩余生存时间。 </returns>
        public async Task<int> TtlAsync(TimeSpan timeSpan, string key)
        {
            var data = await RedisConnection.DoWithKeyAsync(RequestType.TTL, key, timeSpan);

            int.TryParse(data.Data, out int result);

            return result;

        }

        /// <summary>
        /// 以毫秒为单位，返回给定 key 的剩余生存时间(TTL, time to live)
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns>当 key 不存在时，返回 -2 。当 key 存在但没有设置剩余生存时间时，返回 -1 。否则，以秒为单位，返回 key 的剩余生存时间。</returns>
        public async Task<long> PttlAsync(TimeSpan timeSpan, string key)
        {
            var data = await RedisConnection.DoWithKeyAsync(RequestType.PTTL, key, timeSpan);

            long.TryParse(data.Data, out long result);

            return result;
        }

        /// <summary>
        /// 从当前数据库中随机返回(不删除)一个 key
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<string> RandomKeyAsync(TimeSpan timeSpan)
        {
            var data = await RedisConnection.DoAsync(RequestType.RANDOMKEY, timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 将 oldKey 改名为 newkey 。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <returns></returns>
        public async Task<bool> RenameAsync(TimeSpan timeSpan, string oldKey, string newKey)
        {
            var result = await RedisConnection.DoWithKeyValueAsync(RequestType.RENAME, oldKey, newKey, timeSpan);

            if (result.Data == "OK")
            {
                return true;
            }
            return false;
        }

    }
}
