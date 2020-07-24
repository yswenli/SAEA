/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisHashOperationAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/16 17:42:14
*描述：
*=====================================================================
*修改时间：2020/6/16 17:42:14
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// RedisHashOperationAsync
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将哈希表 hash 中域 field 的值设置为 value 
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public async void HSetAsync(TimeSpan timeSpan, string hid, string key, string value)
        {
            await _cnn.DoWithIDAsync(RequestType.HSET, hid, key, value, timeSpan);
        }
        /// <summary>
        /// 同时将多个 field-value (域-值)对设置到哈希表 key 中
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="keyvalues"></param>
        public async void HMSetAsync(TimeSpan timeSpan, string hid, Dictionary<string, string> keyvalues)
        {
            await _cnn.DoBatchWithIDDicAsync(RequestType.HMSET, hid, keyvalues, timeSpan);
        }

        /// <summary>
        /// 返回哈希表中给定域的值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> HGetAsync(TimeSpan timeSpan, string hid, string key)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.HGET, hid, key, timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 返回哈希表 key 中，一个或多个给定域的值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<List<string>> HMGetAsync(TimeSpan timeSpan, string hid, List<string> keys)
        {
            var data = await _cnn.DoBatchWithListAsync(RequestType.HMGET, hid, keys, timeSpan);
            return data.ToList();
        }

        /// <summary>
        /// 返回哈希表 key 中，所有的域和值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> HGetAllAsync(TimeSpan timeSpan, string hid)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.HGETALL, hid, timeSpan);
            return data.ToKeyValues();
        }

        /// <summary>
        /// 返回哈希表 key 中的所有域
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <returns></returns>
        public async Task<List<string>> HGetKeysAsync(TimeSpan timeSpan, string hid)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.HKEYS, hid, timeSpan);
            return data.ToList();
        }

        /// <summary>
        /// 返回哈希表 key 中所有域的值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <returns></returns>
        public async Task<List<string>> HGetValuesAsync(TimeSpan timeSpan, string hid)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.HVALS, hid, timeSpan);
            return data.ToList();
        }

        /// <summary>
        /// 删除哈希表 key 中的一个或多个指定域，不存在的域将被忽略
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<ResponseData> HDelAsync(TimeSpan timeSpan, string hid, string key)
        {
            return await _cnn.DoWithKeyValueAsync(RequestType.HDEL, hid, key, timeSpan);
        }

        /// <summary>
        /// 删除哈希表 key 中的一个或多个指定域，不存在的域将被忽略
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<ResponseData> HDelAsync(TimeSpan timeSpan, string hid, string[] keys)
        {
            return await _cnn.DoBatchWithIDKeysAsync(timeSpan, RequestType.HDEL, hid, keys);
        }

        /// <summary>
        /// 返回哈希表 key 中域的数量
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <returns></returns>
        public async Task<int> HLenAsync(TimeSpan timeSpan, string hid)
        {
            int result;
            var data = await _cnn.DoWithKeyAsync(RequestType.HLEN, hid, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 检查给定域 field 是否存在于哈希表 hash 当中
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<bool> HExistsAsync(TimeSpan timeSpan, string hid, string key)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.HEXISTS, hid, key, timeSpan);
            var result = data.Data;
            return result == "1" ? true : false;
        }

        /// <summary>
        /// 返回哈希表 key 中， 与给定域 field 相关联的值的字符串长度
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<int> HStrLenAsync(TimeSpan timeSpan, string hid, string key)
        {
            int result;

            var data = await _cnn.DoWithKeyValueAsync(RequestType.HSTRLEN, hid, key, timeSpan);

            int.TryParse(data.Data, out result);

            return result;
        }

        /// <summary>
        /// 为哈希表 key 中的域 field 的值加上增量 increment 
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public async Task<long> HIncrementByAsync(TimeSpan timeSpan, string hid, string key, int num)
        {
            var data = await _cnn.DoWithIDAsync(RequestType.HINCRBY, hid, key, num.ToString(), timeSpan);
            return long.Parse(data.Data);
        }

        /// <summary>
        /// 为哈希表 key 中的域 field 加上浮点数增量 increment
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public async Task<float> HIncrementByFloatAsync(TimeSpan timeSpan, string hid, string key, float num)
        {
            var data = await _cnn.DoWithIDAsync(RequestType.HINCRBYFLOAT, hid, key, num.ToString(), timeSpan);
            return float.Parse(data.Data);
        }
    }
}
