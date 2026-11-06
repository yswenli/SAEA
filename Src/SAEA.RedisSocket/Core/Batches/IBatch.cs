/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core.Batches
*文件名： IBatch
*版本号： v26.4.23.1
*唯一标识：df7a90da-34cf-4023-bf81-5eec07b9605d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/24 14:55:42
*描述：IBatch接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
        /// <param name="timeout"></param>
        /// <returns></returns>
        List<string> Execute(int timeout = 10 * 1000);
        /// <summary>
        /// 加入 APPEND 命令，追加内容到键值末尾
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">追加的内容</param>
        void AppendAsync(string key, string value);

        /// <summary>
        /// 加入 DECR 命令，键值减一
        /// </summary>
        /// <param name="key">键名</param>
        void DecrementAsync(string key);

        /// <summary>
        /// 加入 DECRBY 命令，键值减去指定整数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="num">减去的整数</param>
        void DecrementByAsync(string key, int num);

        /// <summary>
        /// 加入 DEL 命令，删除一个或多个键
        /// </summary>
        /// <param name="keys">要删除的键名集合</param>
        void DelAsync(params string[] keys);

        /// <summary>
        /// 加入 EXISTS 命令，判断键是否存在
        /// </summary>
        /// <param name="key">键名</param>
        void ExistsAsync(string key);

        /// <summary>
        /// 加入 EXPIRE 命令，设置键的过期秒数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="seconds">过期时间，单位秒</param>
        void ExpireAsync(string key, int seconds);

        /// <summary>
        /// 加入 EXPIREAT 命令，在指定日期时间过期
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="dateTime">过期日期时间</param>
        void ExpireAtAsync(string key, DateTime dateTime);

        /// <summary>
        /// 加入 EXPIREAT 命令，在指定 Unix 秒级时间戳过期
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="timestamp">过期时间，Unix 秒级时间戳</param>
        void ExpireAtAsync(string key, int timestamp);

        /// <summary>
        /// 加入 GEOADD 命令，添加一个或多个地理位置成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="items">待添加的地理位置成员</param>
        void GeoAddAsync(string key, params GeoItem[] items);

        /// <summary>
        /// 加入 GEODIST 命令，计算两个成员间的距离
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="member1">第一个成员</param>
        /// <param name="member2">第二个成员</param>
        /// <param name="geoUnit">距离单位，默认米</param>
        void GeoDistAsync(string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m);

        /// <summary>
        /// 加入 GET 命令，读取键值
        /// </summary>
        /// <param name="key">键名</param>
        void GetAsync(string key);

        /// <summary>
        /// 加入 GETSET 命令，设置新值并返回旧值
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">新值</param>
        void GetSetAsync(string key, string value);

        /// <summary>
        /// 加入 HDEL 命令，删除哈希表中的一个字段
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        void HDelAsync(string hid, string key);

        /// <summary>
        /// 加入 HDEL 命令，删除哈希表中的多个字段
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="keys">字段名集合</param>
        void HDelAsync(string hid, params string[] keys);

        /// <summary>
        /// 加入 HEXISTS 命令，判断哈希表中字段是否存在
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        void HExistsAsync(string hid, string key);

        /// <summary>
        /// 加入 HGET 命令，读取哈希表中字段的值
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        void HGetAsync(string hid, string key);

        /// <summary>
        /// 加入 HINCRBY 命令，对哈希表字段值加上指定整数
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        /// <param name="num">增加的整数</param>
        void HIncrementByAsync(string hid, string key, int num);

        /// <summary>
        /// 加入 HINCRBYFLOAT 命令，对哈希表字段值加上指定浮点数
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        /// <param name="num">增加的浮点数</param>
        void HIncrementByFloatAsync(string hid, string key, float num);

        /// <summary>
        /// 加入 HLEN 命令，读取哈希表字段数量
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        void HLenAsync(string hid);

        /// <summary>
        /// 加入 HSET 命令，设置哈希表中单个字段的值
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        /// <param name="value">字段值</param>
        void HSetAsync(string hid, string key, string value);

        /// <summary>
        /// 加入 HSTRLEN 命令，读取哈希表字段值的字符串长度
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        void HStrLenAsync(string hid, string key);

        /// <summary>
        /// 加入 INCR 命令，键值加一
        /// </summary>
        /// <param name="key">键名</param>
        void IncrementAsync(string key);

        /// <summary>
        /// 加入 INCRBY 命令，键值加上指定整数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="num">增加的整数</param>
        void IncrementByAsync(string key, int num);

        /// <summary>
        /// 键值加上指定浮点数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="num">增加的浮点数</param>
        void IncrementByFloatAsync(string key, float num);

        /// <summary>
        /// 加入 STRLEN 命令，读取字符串值的长度
        /// </summary>
        /// <param name="key">键名</param>
        void LenAsync(string key);

        /// <summary>
        /// 加入 LINDEX 命令，按下标读取列表元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="index">元素下标，负数表示从表尾开始计数</param>
        void LIndexAsync(string key, int index);

        /// <summary>
        /// 加入 LINSERT 命令，在基准元素前后插入新元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="pivot">基准元素</param>
        /// <param name="isBefore">为 true 插在基准元素之前，否则插在其后</param>
        /// <param name="value">待插入的元素</param>
        void LInsertAsync(string key, string pivot, bool isBefore, string value);

        /// <summary>
        /// 加入 LLEN 命令，读取列表长度
        /// </summary>
        /// <param name="key">键名</param>
        void LLenAsync(string key);

        /// <summary>
        /// 加入 LPOP 命令，移除并返回列表头元素
        /// </summary>
        /// <param name="key">键名</param>
        void LPopAsync(string key);

        /// <summary>
        /// 加入 LPUSH 命令，把多个元素依次插入列表头部
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="lists">待插入的元素集合</param>
        void LPushAsync(string key, List<string> lists);

        /// <summary>
        /// 加入 LPUSH 命令，把单个元素插入列表头部
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">待插入的元素</param>
        void LPushAsync(string key, string value);

        /// <summary>
        /// 加入 LPUSHX 命令，仅当列表存在时把元素插入头部
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">待插入的元素</param>
        void LPushXAsync(string key, string value);

        /// <summary>
        /// 加入 LREM 命令，按数量从列表中移除指定元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="count">移除数量，大于 0 从头到尾移除，小于 0 从尾到头移除，等于 0 移除全部</param>
        /// <param name="value">待移除的元素</param>
        void LRemoveAsync(string key, int count, string value);

        /// <summary>
        /// 加入 LSET 命令，按下标设置列表元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="index">元素下标，负数表示从表尾开始计数</param>
        /// <param name="value">新的元素值</param>
        void LSetAsync(string key, int index, string value);

        /// <summary>
        /// 加入 LTRIM 命令，按区间裁剪列表
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="begin">起始下标，默认 0</param>
        /// <param name="end">结束下标，含该位置，默认 -1 表示表尾</param>
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
        /// <summary>
        /// 加入 RPOPLPUSH 命令，把源列表尾元素移到目标列表头部
        /// </summary>
        /// <param name="source">源列表键名</param>
        /// <param name="destination">目标列表键名</param>
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
        /// <summary>
        /// 加入 SDIFFSTORE 命令，求差集并保存到目标集合
        /// </summary>
        /// <param name="destination">结果集合键名</param>
        /// <param name="keys">参与差集计算的键名集合</param>
        void SDiffStoreAsync(string destination, params string[] keys);

        /// <summary>
        /// 依次加入 SET 与 EXPIRE 命令，写入键值并设置过期时间
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">键值</param>
        /// <param name="seconds">过期时间，单位秒</param>
        void SetAsync(string key, string value, int seconds);

        /// <summary>
        /// 加入 SET 命令，写入键值且不设过期时间
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">键值</param>
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
        /// <summary>
        /// 加入 SPOP 命令，随机移除并返回集合中一个元素
        /// </summary>
        /// <param name="key">键名</param>
        void SPopAsync(string key);

        /// <summary>
        /// 加入 SRANDMEMBER 命令，随机返回集合中一个元素且不删除
        /// </summary>
        /// <param name="key">键名</param>
        void SRandMemeberAsync(string key);

        /// <summary>
        /// 加入 SREM 命令，从集合中移除一个或多个成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="values">待移除的成员集合</param>
        void SRemoveAsync(string key, params string[] values);

        /// <summary>
        /// 加入 SUNIONSTORE 命令，求并集并保存到目标集合
        /// </summary>
        /// <param name="destination">结果集合键名</param>
        /// <param name="keys">参与并集计算的键名集合</param>
        void SUnionStoreAsync(string destination, params string[] keys);

        /// <summary>
        /// 加入 TTL 命令，读取键的剩余生存时间
        /// </summary>
        /// <param name="key">键名</param>
        void TtlAsync(string key);

        /// <summary>
        /// 加入 ZADD 命令，按分值批量添加有序集成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="scoreVals">分值到成员名的映射</param>
        void ZAddAsync(string key, Dictionary<double, string> scoreVals);

        /// <summary>
        /// 加入 ZADD 命令，添加单个有序集成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">成员名</param>
        /// <param name="score">成员分值</param>
        void ZAddAsync(string key, string value, double score);

        /// <summary>
        /// 加入 ZCOUNT 命令，统计分值区间内的成员数量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="begin">分值区间下界，含边界，默认 int 最小值</param>
        /// <param name="end">分值区间上界，含边界，默认 int 最大值</param>
        void ZCountAsync(string key, double begin = -2147483648, double end = 2147483647);

        /// <summary>
        /// 加入 ZINCRBY 命令，对指定成员的分值加上浮点增量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="increment">分值增量</param>
        /// <param name="value">成员名</param>
        void ZIncrByAsync(string key, double increment, string value);

        /// <summary>
        /// 加入 ZINCRBY 命令，对指定成员的分值加上整数增量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="increment">分值增量</param>
        /// <param name="value">成员名</param>
        void ZIncrByAsync(string key, long increment, string value);

        /// <summary>
        /// 加入 ZCARD 命令，读取有序集的成员数量
        /// </summary>
        /// <param name="key">键名</param>
        void ZLenAsync(string key);

        /// <summary>
        /// 加入 ZLEXCOUNT 命令，统计字典序区间内的成员数量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="min">字典序下界，默认 double 最小值</param>
        /// <param name="max">字典序上界，默认 double 最大值</param>
        /// <param name="offset">偏移量，默认 -1</param>
        /// <param name="count">统计数量上限，默认 20</param>
        void ZLexCountAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20);
        /// <summary>
        /// 返回有序集 key 中成员 member 的排名
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void ZRankAsync(string key, string value);
        /// <summary>
        /// 加入 ZREM 命令，移除有序集中的一个或多个成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="values">待移除的成员集合</param>
        void ZRemoveAsync(string key, params string[] values);
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

        /// <summary>
        /// 释放批量队列，清空已缓存的命令
        /// </summary>
        void Dispose();
    }
}