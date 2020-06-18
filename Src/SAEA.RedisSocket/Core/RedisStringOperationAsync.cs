/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisStringOperationAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/17 16:57:36
*描述：
*=====================================================================
*修改时间：2020/6/17 16:57:36
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
    /// string 操作 ,RedisStringOperationAsync
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将字符串值 value 关联到 key 
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public async void SetAsync(TimeSpan timeSpan, string key, string value)
        {
            await _cnn.DoWithKeyValueAsync(RequestType.SET, key, value, timeSpan);
        }

        /// <summary>
        /// 将字符串值 value 关联到 key 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="seconds"></param>
        public async void SetAsync(string key, string value, int seconds)
        {
            await _cnn.DoExpireInsertAsync(RequestType.SET, key, value, seconds);
        }

        /// <summary>
        /// 将字符串值 value 关联到 key
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="dic"></param>
        public async void MSetAsync(TimeSpan timeSpan, Dictionary<string, string> dic)
        {
            await _cnn.DoBatchWithDicAsync(RequestType.MSET, dic, timeSpan);
        }

        /// <summary>
        /// 当且仅当所有给定键都不存在时， 为所有给定键设置值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="dic"></param>
        public async void MSetNxAsync(TimeSpan timeSpan, Dictionary<string, string> dic)
        {
            await _cnn.DoBatchWithDicAsync(RequestType.MSETNX, dic, timeSpan);
        }

        /// <summary>
        /// 如果键 key 已经存在并且它的值是一个字符串， APPEND 命令将把 value 追加到键 key 现有值的末尾
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> AppendAsync(TimeSpan timeSpan, string key, string value)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.APPEND, key, value, timeSpan);
            return int.Parse(data.Data);
        }

        /// <summary>
        /// 返回 key 所关联的字符串值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> GetAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.GET, key, timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 返回 key 所关联的字符串值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<List<string>> MGetAsync(TimeSpan timeSpan, params string[] keys)
        {
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.MGET, timeSpan, keys);
            return data.ToList();
        }

        /// <summary>
        /// 如果 key 已经持有其他值， SET 就覆写旧值，无视类型
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<string> GetSetAsync(TimeSpan timeSpan, string key, string value)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.GETSET, key, value, timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 为键 key 储存的数字值加上一
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<long> IncrementAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.INCR, key, timeSpan);

            return long.Parse(data.Data);
        }

        /// <summary>
        /// 为键 key 储存的数字值减去一
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<long> DecrementAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.DECR, key, timeSpan);
            return long.Parse(data.Data);
        }

        /// <summary>
        /// 为键 key 储存的数字值加上增量 increment 
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public async Task<long> IncrementByAsync(TimeSpan timeSpan, string key, int num)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.INCRBY, key, num.ToString(), timeSpan);
            return long.Parse(data.Data);
        }

        /// <summary>
        /// 为键 key 储存的数字值加上增量 increment 
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public async Task<long> DecrementByAsync(TimeSpan timeSpan, string key, int num)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.DECRBY, key, num.ToString(), timeSpan);
            return long.Parse(data.Data);
        }

        /// <summary>
        /// 为键 key 储存的值加上浮点数增量 increment
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public async Task<float> IncrementByFloatAsync(TimeSpan timeSpan, string key, float num)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.INCRBYFLOAT, key, num.ToString(), timeSpan);
            return float.Parse(data.Data);
        }

        /// <summary>
        /// 返回键 key 储存的字符串值的长度
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<int> LenAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.STRLEN, key, timeSpan);
            return int.Parse(data.Data);
        }
    }
}
