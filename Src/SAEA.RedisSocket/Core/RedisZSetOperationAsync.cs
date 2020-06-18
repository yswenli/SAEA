/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisZSetOperationAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/18 9:55:20
*描述：
*=====================================================================
*修改时间：2020/6/18 9:55:20
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// ZSet,
    /// RedisZSetOperationAsync
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将一个或多个 member 元素及其 score 值加入到有序集 key 当中
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="score"></param>
        public async void ZAddAsync(TimeSpan timeSpan, string key, string value, double score)
        {
            await _cnn.DoWithIDAsync(RequestType.ZADD, key, score.ToString(), value, timeSpan);
        }

        /// <summary>
        /// 将一个或多个 member 元素及其 score 值加入到有序集 key 当中
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="scoreVals"></param>
        public async void ZAddAsync(TimeSpan timeSpan, string key, Dictionary<double, string> scoreVals)
        {
            await _cnn.DoBatchZaddWithIDDicAsync(RequestType.ZADD, key, scoreVals, timeSpan);
        }

        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<double> ZScoreAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0D;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.ZSCORE, key, value, timeSpan);
            double.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 为有序集 key 的成员 member 的 score 值加上增量 increment
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="increment"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<long> ZIncrByAsync(TimeSpan timeSpan, string key, long increment, string value)
        {
            var result = 0L;
            var data = await _cnn.DoWithIDAsync(RequestType.ZINCRBY, key, increment.ToString(), value, timeSpan);
            long.TryParse(data.Data, out result);
            return result;

        }

        /// <summary>
        /// 为有序集 key 的成员 member 的 score 值加上增量 increment
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="increment"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<double> ZIncrByAsync(TimeSpan timeSpan, string key, double increment, string value)
        {
            var result = 0D;
            var data = await _cnn.DoWithIDAsync(RequestType.ZINCRBY, key, increment.ToString(), value, timeSpan);
            double.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 的基数
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<int> ZLenAsync(TimeSpan timeSpan, string key)
        {
            var result = 0;
            var data = await _cnn.DoWithKeyAsync(RequestType.ZCARD, key, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 中， score 值在 min 和 max 之间(默认包括 score 值等于 min 或 max )的成员的数量。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public async Task<int> ZCountAsync(TimeSpan timeSpan, string key, double begin = int.MinValue, double end = int.MaxValue)
        {
            var result = 0;
            var data = await _cnn.DoWithIDAsync(RequestType.ZCOUNT, key, begin.ToString(), end.ToString(), timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。
        /// 其中成员的位置按 score 值递增(从小到大)来排序
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public async Task<List<ZItem>> ZRangeAsync(TimeSpan timeSpan, string key, double start = 0, double stop = -1)
        {
            var data = await _cnn.DoRangAsync(RequestType.ZRANGE, key, start, stop, timeSpan);
            return data.ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。
        /// 其中成员的位置按 score 值递增(从大到小)来排序。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public async Task<List<ZItem>> ZRevrangeAsync(TimeSpan timeSpan, string key, double start = 0, double stop = -1)
        {
            var data = await _cnn.DoRangAsync(RequestType.ZREVRANGE, key, start, stop, timeSpan);
            return data.ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员。有序集成员按 score 值递增(从小到大)次序排列。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<List<ZItem>> ZRangeByScoreAsync(TimeSpan timeSpan, string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20)
        {
            var data = await _cnn.DoRangByScoreAsync(timeSpan, RequestType.ZRANGEBYSCORE, key, min, max, rangType, offset, count, true);
            return data.ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中， score 值介于 max 和 min 之间(默认包括等于 max 或 min )的所有的成员。有序集成员按 score 值递减(从大到小)的次序排列。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<List<ZItem>> ZRevRangeByScoreAsync(TimeSpan timeSpan, string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20)
        {
            var data = await _cnn.DoRangByScoreAsync(timeSpan, RequestType.ZREVRANGEBYSCORE, key, min, max, rangType, offset, count, true);
            return data.ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递增(从小到大)顺序排列。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<long> ZRankAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0L;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.ZRANK, key, value, timeSpan);
            long.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递减(从大到小)排序。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<long> ZRevRankAsync(TimeSpan timeSpan, string key, string value)
        {
            var result = 0L;
            var data = await _cnn.DoWithKeyValueAsync(RequestType.ZREVRANK, key, value, timeSpan);
            long.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 移除有序集 key 中的一个或多个成员，不存在的成员将被忽略。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task<int> ZRemoveAsync(TimeSpan timeSpan, string key, string[] values)
        {
            var result = 0;
            var data = await _cnn.DoBatchWithIDKeysAsync(timeSpan, RequestType.ZREM, key, values);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public async Task<int> ZRemoveByRankAsync(TimeSpan timeSpan, string key, double start = 0, double stop = -1)
        {
            var result = 0;
            var data = await _cnn.DoRangAsync(RequestType.ZREMRANGEBYRANK, key, start, stop, timeSpan);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public async Task<int> ZRemoveByScoreAsync(TimeSpan timeSpan, string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None)
        {
            var result = 0;
            var data = await _cnn.DoRangByScoreAsync(timeSpan, RequestType.ZREMRANGEBYSCORE, key, min, max, rangType);
            int.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 返回指定成员区间内的成员,
        /// 此指令适用于分数相同的有序集合中,
        /// LEX结尾的指令是要求分数必须相同
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<List<string>> ZrangeByLexAsync(TimeSpan timeSpan, string key, double min, double max, long offset = -1, int count = 20)
        {
            var data = await _cnn.DoRangByScoreAsync(timeSpan, RequestType.ZRANGEBYLEX, key, min, max, RangType.None, offset, count);
            return data.ToList();
        }

        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会返回该集合中， 成员介于 min 和 max 范围内的元素数量。
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<long> ZLexCountAsync(TimeSpan timeSpan, string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            long result = 0;
            var data = await _cnn.DoRangByScoreAsync(timeSpan, RequestType.ZLEXCOUNT, key, min, max, RangType.None, offset, count);
            long.TryParse(data.Data, out result);
            return result;
        }

        /// <summary>
        /// 移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// 所有成员的分值都相同的有序集合键 key
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task<long> ZRemoveByLexAsync(TimeSpan timeSpan, string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            long result = 0;
            var data = await _cnn.DoRangByScoreAsync(timeSpan, RequestType.ZREMRANGEBYLEX, key, min, max, RangType.None, offset, count);
            long.TryParse(data.Data, out result);
            return result;
        }
    }
}
