/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisListOperationAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/17 14:51:57
*描述：
*=====================================================================
*修改时间：2020/6/17 14:51:57
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// RedisListOperationAsync
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将一个或多个值 value 插入到列表 key 的表头
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> LPushAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.LPUSH, key, value, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 将一个或多个值 value 插入到列表 key 的表头
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="lists"></param>
        /// <returns></returns>
        public async Task<int> LPushAsync(TimeSpan timeSpan, string key, List<string> lists)
        {
            var result = 0;
            var data = await _cnn.DoBatchWithListAsync(RequestType.LPUSH, key, lists, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 将值 value 插入到列表 key 的表头，当且仅当 key 存在并且是一个列表
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> LPushXAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.LPUSHX, key, value, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 将一个或多个值 value 插入到列表 key 的表尾(最右边)
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> RPushAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.RPUSH, key, value, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        ///  将一个或多个值 value 插入到列表 key 的表尾(最右边)
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task<int> RPushAsync(TimeSpan timeSpan, string key, List<string> values)
        {
            var result = 0;
            var data = await _cnn.DoBatchWithListAsync(RequestType.RPUSH, key, values, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 将值 value 插入到列表 key 的表尾，当且仅当 key 存在并且是一个列表
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> RPushXAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.RPUSHX, key, value, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 移除并返回列表 key 的头元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> LPopAsync(TimeSpan timeSpan, string key)
        {
            return _cnn.DoWithKey(RequestType.LPOP, key).Data;
        }

        /// <summary>
        /// 移除并返回列表 key 的尾元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> RPopAsync(TimeSpan timeSpan, string key)
        {
            var data = await _cnn.DoWithKeyAsync(RequestType.RPOP, key, timeSpan);

            return data.Data;
        }

        /// <summary>
        /// 原子操作两个队列
        /// 将列表 source 中的最后一个元素(尾元素)弹出，并返回给客户端,
        /// 将 source 弹出的元素插入到列表 destination ，作为 destination 列表的的头元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public async Task<string> RpopLPushAsync(TimeSpan timeSpan, string source, string destination)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.RPOPLPUSH, source, destination, timeSpan);

            return data.Data;
        }

        /// <summary>
        /// 根据参数 count 的值，移除列表中与参数 value 相等的元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> LRemoveAsync(TimeSpan timeSpan, string key, int count, string value)
        {
            var result = 0;
            var data = await _cnn.DoWithIDAsync(RequestType.LREM, key, count.ToString(), value, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回列表 key 的长度
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<int> LLenAsync(TimeSpan timeSpan, string key)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyAsync(RequestType.LLEN, key, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回列表 key 中，下标为 index 的元素
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public async Task<string> LIndexAsync(TimeSpan timeSpan, string key, int index)
        {
            var data = await _cnn.DoWithKeyValueAsync(RequestType.LINDEX, key, index.ToString(), timeSpan);
            return data.Data;
        }

        /// <summary>
        /// 将值 value 插入到列表 key 当中，位于值 pivot 之前或之后
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="pivot"></param>
        /// <param name="isBefore"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<int> LInsertAsync(TimeSpan timeSpan, string key, string pivot, bool isBefore, string value)
        {
            key.KeyCheck();
            var beforStr = isBefore ? "BEFORE" : "AFTER";
            var list = new List<string>();
            list.Add(key);
            list.Add(beforStr);
            list.Add(pivot);
            list.Add(value);
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.LINSERT, timeSpan, list.ToArray());
            return int.Parse(data.Data);
        }

        /// <summary>
        /// 将列表 key 下标为 index 的元素的值设置为 value
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public async void LSetAsync(TimeSpan timeSpan, string key, int index, string value)
        {
            await _cnn.DoWithIDAsync(RequestType.LSET, key, index.ToString(), value, timeSpan);
        }


        /// <summary>
        /// 返回列表 key 中指定区间内的元素，区间以偏移量 start 和 stop 指定
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public async Task<List<string>> LRangAsync(TimeSpan timeSpan, string key, int begin = 0, int end = -1)
        {
            var data = await _cnn.DoWithIDAsync(RequestType.LRANGE, key, begin.ToString(), end.ToString(), timeSpan);
            return data.ToList();
        }

        /// <summary>
        /// 对一个列表进行修剪(trim)，就是说，让列表只保留指定区间内的元素，不在指定区间之内的元素都将被删除。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public async Task<bool> LTrimAsync(TimeSpan timeSpan, string key, int begin = 0, int end = -1)
        {
            var data = await _cnn.DoWithIDAsync(RequestType.LTRIM, key, begin.ToString(), end.ToString(), timeSpan);
            return data.Data.IndexOf("OK") > -1 ? true : false;
        }

        /// <summary>
        /// 它是 LPOP key 命令的阻塞版本，当给定列表内没有任何元素可供弹出的时候，连接将被 BLPOP 命令阻塞，直到等待超时或发现可弹出元素为止。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async Task<List<string>> BLPopAsync(TimeSpan timeSpan, List<string> keys, int seconds = 0)
        {
            keys.NotNull();
            List<string> datas = new List<string>();
            datas.AddRange(keys);
            datas.Add(seconds.ToString());
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.BLPOP, timeSpan, datas.ToArray());
            return data.ToList();
        }

        /// <summary>
        /// 它是 LPOP key 命令的阻塞版本，当给定列表内没有任何元素可供弹出的时候，连接将被 BLPOP 命令阻塞，直到等待超时或发现可弹出元素为止。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async Task<List<string>> BLPopAsync(TimeSpan timeSpan, string key, int seconds = 0)
        {
            return await BLPopAsync(timeSpan, new List<string>() { key }, seconds);
        }

        /// <summary>
        /// 它是 RPOP key 命令的阻塞版本，当给定列表内没有任何元素可供弹出的时候，连接将被 BRPOP 命令阻塞，直到等待超时或发现可弹出元素为止。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="keys"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async Task<List<string>> BRPopAsync(TimeSpan timeSpan, List<string> keys, int seconds = 0)
        {
            keys.NotNull();
            List<string> datas = new List<string>();
            datas.AddRange(keys);
            datas.Add(seconds.ToString());
            var data = await _cnn.DoWithMutiParamsAsync(RequestType.BRPOP, timeSpan, datas.ToArray());
            return data.ToList();
        }

        /// <summary>
        /// 它是 RPOP key 命令的阻塞版本，当给定列表内没有任何元素可供弹出的时候，连接将被 BRPOP 命令阻塞，直到等待超时或发现可弹出元素为止。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async Task<List<string>> BRPopAsync(TimeSpan timeSpan, string key, int seconds = 0)
        {
            return await BRPopAsync(timeSpan, new List<string>() { key }, seconds);
        }

        /// <summary>
        /// BRPOPLPUSH 是 RPOPLPUSH source destination 的阻塞版本，当给定列表 source 不为空时， BRPOPLPUSH 的表现和 RPOPLPUSH source destination 一样。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async Task<string> BRPopLPushAsync(TimeSpan timeSpan, string source, string destination, int seconds = 0)
        {
            var data = await _cnn.DoWithIDAsync(RequestType.BRPOPLPUSH, source, destination, seconds.ToString(), timeSpan);
            return data.Data;
        }

    }
}
