/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisZSetOperation
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/16 9:47:26
*描述：
*=====================================================================
*修改时间：2019/8/16 9:47:26
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// ZSet
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将一个或多个 member 元素及其 score 值加入到有序集 key 当中
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="score"></param>
        public void ZAdd(string key, string value, double score)
        {
            RedisConnection.DoWithID(RequestType.ZADD, key, score.ToString(), value);
        }

        /// <summary>
        /// 将一个或多个 member 元素及其 score 值加入到有序集 key 当中
        /// </summary>
        /// <param name="key"></param>
        /// <param name="scoreVals"></param>
        public void ZAdd(string key, Dictionary<double, string> scoreVals)
        {
            RedisConnection.DoBatchZaddWithIDDic(RequestType.ZADD, key, scoreVals);
        }

        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public double ZScore(string key, string value)
        {
            var result = 0D;
            double.TryParse(RedisConnection.DoWithKeyValue(RequestType.ZSCORE, key, value).Data, out result);
            return result;
        }

        /// <summary>
        /// 为有序集 key 的成员 member 的 score 值加上增量 increment
        /// </summary>
        /// <param name="key"></param>
        /// <param name="increment"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public long ZIncrBy(string key, long increment, string value)
        {
            var result = 0L;
            long.TryParse(RedisConnection.DoWithID(RequestType.ZINCRBY, key, increment.ToString(), value).Data, out result);
            return result;

        }

        /// <summary>
        /// 为有序集 key 的成员 member 的 score 值加上增量 increment
        /// </summary>
        /// <param name="key"></param>
        /// <param name="increment"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public double ZIncrBy(string key, double increment, string value)
        {
            var result = 0D;
            double.TryParse(RedisConnection.DoWithID(RequestType.ZINCRBY, key, increment.ToString(), value).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 的基数
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int ZLen(string key)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoWithKey(RequestType.ZCARD, key).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 中， score 值在 min 和 max 之间(默认包括 score 值等于 min 或 max )的成员的数量。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public int ZCount(string key, double begin = int.MinValue, double end = int.MaxValue)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoWithID(RequestType.ZCOUNT, key, begin.ToString(), end.ToString()).Data, out result);
            return result;
        }
        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。
        /// 其中成员的位置按 score 值递增(从小到大)来排序
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>

        public List<ZItem> ZRange(string key, double start = 0, double stop = -1)
        {
            return RedisConnection.DoRang(RequestType.ZRANGE, key, start, stop).ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。
        /// 其中成员的位置按 score 值递增(从大到小)来排序。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public List<ZItem> ZRevrange(string key, double start = 0, double stop = -1)
        {
            return RedisConnection.DoRang(RequestType.ZREVRANGE, key, start, stop).ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员。有序集成员按 score 值递增(从小到大)次序排列。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<ZItem> ZRangeByScore(string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20)
        {
            return RedisConnection.DoRangByScore(RequestType.ZRANGEBYSCORE, key, min, max, rangType, offset, count, true).ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中， score 值介于 max 和 min 之间(默认包括等于 max 或 min )的所有的成员。有序集成员按 score 值递减(从大到小)的次序排列。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<ZItem> ZRevRangeByScore(string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20)
        {
            return RedisConnection.DoRangByScore(RequestType.ZREVRANGEBYSCORE, key, min, max, rangType, offset, count, true).ToZList();
        }

        /// <summary>
        /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递增(从小到大)顺序排列。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public long ZRank(string key, string value)
        {
            var result = 0L;
            long.TryParse(RedisConnection.DoWithKeyValue(RequestType.ZRANK, key, value).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递减(从大到小)排序。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public long ZRevRank(string key, string value)
        {
            var result = 0L;
            long.TryParse(RedisConnection.DoWithKeyValue(RequestType.ZREVRANK, key, value).Data, out result);
            return result;
        }

        /// <summary>
        /// 移除有序集 key 中的一个或多个成员，不存在的成员将被忽略。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public int ZRemove(string key, params string[] values)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoBatchWithIDKeys(RequestType.ZREM, key, values).Data, out result);
            return result;
        }

        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public int ZRemoveByRank(string key, double start = 0, double stop = -1)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoRang(RequestType.ZREMRANGEBYRANK, key, start, stop).Data, out result);
            return result;
        }

        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public int ZRemoveByScore(string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoRangByScore(RequestType.ZREMRANGEBYSCORE, key, min, max, rangType).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回指定成员区间内的成员,
        /// 此指令适用于分数相同的有序集合中,
        /// LEX结尾的指令是要求分数必须相同
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<string> ZrangeByLex(string key, double min, double max, long offset = -1, int count = 20)
        {
            return RedisConnection.DoRangByScore(RequestType.ZRANGEBYLEX, key, min, max, RangType.None, offset, count).ToList();
        }

        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会返回该集合中， 成员介于 min 和 max 范围内的元素数量。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public long ZLexCount(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            long result = 0;
            long.TryParse(RedisConnection.DoRangByScore(RequestType.ZLEXCOUNT, key, min, max, RangType.None, offset, count).Data, out result);
            return result;
        }

        /// <summary>
        /// 移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// 所有成员的分值都相同的有序集合键 key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public long ZRemoveByLex(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            long result = 0;
            long.TryParse(RedisConnection.DoRangByScore(RequestType.ZREMRANGEBYLEX, key, min, max, RangType.None, offset, count).Data, out result);
            return result;
        }
    }
}
