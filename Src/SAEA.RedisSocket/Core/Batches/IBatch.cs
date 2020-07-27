/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisDataBase
*版本号： v5.0.0.1
*唯一标识：3d4f939c-3fb9-40e9-a0e0-c7ec773539ae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/22 10:37:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/22 10:37:15
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core.Batches
{
    /// <summary>
    /// 批量操作方法
    /// </summary>
    public interface IBatch
    {
        /// <summary>
        /// 执行操作
        /// </summary>
        /// <returns></returns>
        IEnumerable<object> Execute();
        void AppendAsync(string key, string value);        

        void DecrementAsync(string key);
        void DecrementByAsync(string key, int num);
        void DelAsync(params string[] keys);
        void ExistsAsync(string key);
        void ExpireAsync(string key, int seconds);
        void ExpireAtAsync(string key, DateTime dateTime);
        void ExpireAtAsync(string key, int timestamp);
        void GeoAddAsync(string key, params GeoItem[] items);
        void GeoDistAsync(string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m);
        void GetAsync(string key);
        void GetSetAsync(string key, string value);
        void HDelAsync(string hid, string key);
        void HDelAsync(string hid, string[] keys);
        void HExistsAsync(string hid, string key);
        void HGetAsync(string hid, string key);
        void HIncrementByAsync(string hid, string key, int num);
        void HIncrementByFloatAsync(string hid, string key, float num);
        void HLenAsync(string hid);
        void HSetAsync(string hid, string key, string value);
        void HStrLenAsync(string hid, string key);
        void IncrementAsync(string key);
        void IncrementByAsync(string key, int num);
        void IncrementByFloatAsync(string key, float num);
        void LenAsync(string key);
        void LIndexAsync(string key, int index);
        void LInsertAsync(string key, string pivot, bool isBefore, string value);
        void LLenAsync(string key);
        void LPopAsync(string key);
        void LPushAsync(string key, List<string> lists);
        void LPushAsync(string key, string value);
        void LPushXAsync(string key, string value);
        void LRemoveAsync(string key, int count, string value);
        void LSetAsync(string key, int index, string value);
        void LTrimAsync(string key, int begin = 0, int end = -1);
        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="key"></param>
        void PersistAsync(string key);
        /// <summary>
        /// 这个命令类似于 TTL 命令，但它以毫秒为单位返回 key 的剩余生存时间，而不是像 TTL 命令那样，以秒为单位。
        /// </summary>
        /// <param name="key"></param>
        void PttlAsync(string key);
        /// <summary>
        /// 从当前数据库中随机返回(不删除)一个 key 。
        /// </summary>
        void RandomKeyAsync();
        /// <summary>
        /// 将 key 改名为 newkey 。
        /// 当 key 和 newkey 相同，或者 key 不存在时，返回一个错误。
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        void RenameAsync(string oldKey, string newKey);
        /// <summary>
        /// 移除并返回列表 key 的尾元素
        /// </summary>
        /// <param name="key"></param>
        void RPopAsync(string key);
        void RpopLPushAsync(string source, string destination);
        /// <summary>
        /// 将一个或多个值 value 插入到列表 key 的表尾(最右边)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void RPushAsync(string key, params string[] values);
        /// <summary>
        /// 将值 value 插入到列表 key 的表尾，当且仅当 key 存在并且是一个列表。        
        /// 和 RPUSH 命令相反，当 key 不存在时， RPUSHX 命令什么也不做。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void RPushXAsync(string key, string value);
        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        void SAddAsync(string key, params string[] values);
        void SDiffStoreAsync(string destination, params string[] keys);
        void SetAsync(string key, string value, int seconds);
        void SetAsync(string key, string value);
        /// <summary>
        /// 判断 member 元素是否集合 key 的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SExistsAsync(string key, string value);
        /// <summary>
        /// 返回集合交集数量并保存到 destination 集合
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        void SInterStoreAsync(string destination, params string[] keys);
        /// <summary>
        /// 返回集合 key 的基数(集合中元素的数量)。
        /// </summary>
        /// <param name="key"></param>
        void SLenAsync(string key);
        void SPopAsync(string key);
        void SRandMemeberAsync(string key);
        void SRemoveAsync(string key, params string[] values);
        void SUnionStoreAsync(string destination, params string[] keys);
        void TtlAsync(string key);
        void ZAddAsync(string key, Dictionary<double, string> scoreVals);
        void ZAddAsync(string key, string value, double score);
        void ZCountAsync(string key, double begin = -2147483648, double end = 2147483647);
        void ZIncrByAsync(string key, double increment, string value);
        void ZIncrByAsync(string key, long increment, string value);
        void ZLenAsync(string key);
        void ZLexCountAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20);
        /// <summary>
        /// 返回有序集 key 中成员 member 的排名
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void ZRankAsync(string key, string value);
        void ZRemoveAsync(string key, string[] values);
        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void ZRemoveByLexAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20);
        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        void ZRemoveByRankAsync(string key, double start = 0, double stop = -1);
        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        void ZRemoveByScoreAsync(string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None);
        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。成员按 score 值递减
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void ZRevRankAsync(string key, string value);
        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void ZScoreAsync(string key, string value);
    }
}