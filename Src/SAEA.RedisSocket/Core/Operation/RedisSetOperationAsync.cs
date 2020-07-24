/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisSetOperationAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/17 16:12:22
*描述：
*=====================================================================
*修改时间：2020/6/17 16:12:22
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// RedisSetOperationAsync
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public async void SAddAsync(TimeSpan timeSpan, string key, string value)
        {
            await _cnn.DoWithKeyValueAsync(RequestType.SADD, key, value, timeSpan);
        }

        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public async void SAddAsync(TimeSpan timeSpan, string key, string[] value)
        {
            await _cnn.DoBatchWithIDKeysAsync(timeSpan, RequestType.SADD, key, value);
        }

        /// <summary>
        /// 判断 member 元素是否集合 key 的成员
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<bool> SExistsAsync(TimeSpan timeSpan, string key, string value)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.SISMEMBER, key, value, timeSpan);
            var result = data.Data;
            return result.IndexOf("1") > -1 ? true : false;
        }

        /// <summary>
        /// 移除并返回集合中的一个随机元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> SPopAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.SPOP, key, timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 返回集合中的一个随机元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> SRandMemeberAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.SRANDMEMBER, key, timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 移除集合 key 中的一个或多个 member 元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task<int> SRemoveAsync(TimeSpan timeSpan, string key, params string[] values)
        {
            var result = 0;
            var data = await _cnn.DoBatchWithIDKeysAsync(timeSpan, RequestType.SREM, key, values);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 将 member 元素从 source 集合移动到 destination 集合
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task<int> SMove(TimeSpan timeSpan, string source, string destination, string key)
        {
            var result = 0;
            var data = await _cnn.DoWithIDAsync(RequestType.SMOVE, source, destination, key, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 回集合 key 的基数(集合中元素的数量)
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<int> SLenAsync(TimeSpan timeSpan, string key)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyAsync(RequestType.SCARD, key, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回集合 key 中的所有成员
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<List<string>> SMemebersAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.SMEMBERS, key, timeSpan);
            return data.ToList();
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的交集
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<List<string>> SInterAsync(TimeSpan timeSpan, params string[] keys)
        {
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.SINTER, timeSpan, keys);
            return data.ToList();
        }

        /// <summary>
        /// 类似于 SINTER key [key …] 命令，但它将结果保存到 destination 集合，而不是简单地返回结果集
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <returns>结果集中的成员数量</returns>
        public async Task<int> SInterStoreAsync(TimeSpan timeSpan, string destination, params string[] keys)
        {
            keys.NotNull();
            var result = 0;
            var data = await _cnn.DoBatchWithListAsync(RequestType.SINTERSTORE, destination, keys.ToList(), timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的并集
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<List<string>> SUnionAsync(TimeSpan timeSpan, params string[] keys)
        {
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.SUNION, timeSpan, keys);
            return data.ToList();
        }

        /// <summary>
        /// 类似于 SUNION key [key …] 命令，但它将结果保存到 destination 集合，而不是简单地返回结果集
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <returns>结果集中的成员数量</returns>
        public async Task<int> SUnionStoreAsync(TimeSpan timeSpan, string destination, params string[] keys)
        {
            keys.NotNull();
            var result = 0;
            var data = await _cnn.DoBatchWithListAsync(RequestType.SUNIONSTORE, destination, keys, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合之间的差集
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<List<string>> SDiffAsync(TimeSpan timeSpan, params string[] keys)
        {
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.SDIFF, timeSpan, keys);
            return data.ToList();
        }

        /// <summary>
        /// 类似于 SDIFF key [key …] 命令，但它将结果保存到 destination 集合，而不是简单地返回结果集
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <returns>结果集中的成员数量</returns>
        public async Task<int> SDiffStoreAsync(TimeSpan timeSpan, string destination, params string[] keys)
        {
            keys.NotNull();
            var result = 0;
            var data = await _cnn.DoBatchWithListAsync(RequestType.SDIFFSTORE, destination, keys.ToList(), timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

    }
}
