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
*文件名： Batch
*版本号： v26.4.23.1
*唯一标识：5717a05a-5af3-47f9-b4cf-fe6876fa753e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/24 09:50:43
*描述：Batch类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RedisSocket.Core.Batches
{
    /// <summary>
    /// Redis批量操作类
    /// </summary>
    public class Batch : IBatch, IDisposable
    {
        List<BatchItem> _batchData;

        RedisCoder _redisCode;

        readonly object SyncRoot;

        /// <summary>
        /// Redis批量操作类
        /// </summary>
        /// <param name="redisDataBase"></param>
        internal Batch(RedisDataBase redisDataBase)
        {
            _redisCode = redisDataBase.RedisConnection.RedisCoder;

            SyncRoot = redisDataBase.RedisConnection.SyncRoot;

            _batchData = new List<BatchItem>();
        }
        /// <summary>
        /// 执行批量操作
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public List<string> Execute(int timeout = 10 * 1000)
        {
            return TaskHelper.Run((token) =>
            {
                List<string> result = null;

                lock (SyncRoot)
                {
                    if (!_batchData.Any()) return null;

                    result = new List<string>();

                    StringBuilder sb = new StringBuilder();

                    foreach (var item in _batchData)
                    {
                        sb.Append(item.Cmd);
                    }

                    _redisCode.Request(sb.ToString());

                    foreach (var item in _batchData)
                    {
                        var data = _redisCode.Decoder<string>(item.RequestType, token);

                        result.Add(data.Data);
                    }
                }
                return result;

            }, timeout).GetAwaiter().GetResult();

        }

        /// <summary>
        /// 将 APPEND 命令加入批量队列
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">追加到键值末尾的内容</param>
        public void AppendAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.APPEND, key, value);

            _batchData.Add(new BatchItem(RequestType.APPEND, cmd));
        }


        /// <summary>
        /// 将 DECR 命令加入批量队列，键值减一
        /// </summary>
        /// <param name="key">键名</param>
        public void DecrementAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.DECR, key);

            _batchData.Add(new BatchItem(RequestType.DECR, cmd));
        }

        /// <summary>
        /// 将 DECRBY 命令加入批量队列，键值减去指定整数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="num">减去的整数</param>
        public void DecrementByAsync(string key, int num)
        {
            var cmd = _redisCode.Coder(RequestType.DECRBY, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.DECRBY, cmd));
        }

        /// <summary>
        /// 将 DEL 命令加入批量队列，删除一个或多个键
        /// </summary>
        /// <param name="keys">要删除的键名集合</param>
        public void DelAsync(params string[] keys)
        {
            var cmd = _redisCode.Coder(RequestType.DEL, keys);

            _batchData.Add(new BatchItem(RequestType.DEL, cmd));
        }

        /// <summary>
        /// 将 EXISTS 命令加入批量队列，判断键是否存在
        /// </summary>
        /// <param name="key">键名</param>
        public void ExistsAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.EXISTS, key);

            _batchData.Add(new BatchItem(RequestType.EXISTS, cmd));
        }

        /// <summary>
        /// 将 EXPIRE 命令加入批量队列，设置键的过期秒数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="seconds">过期时间，单位秒</param>
        public void ExpireAsync(string key, int seconds)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIRE, key, seconds.ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIRE, cmd));
        }

        /// <summary>
        /// 将 EXPIREAT 命令加入批量队列，在指定日期时间过期
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="dateTime">过期日期时间，按本地时区换算为 Unix 秒级时间戳</param>
        public void ExpireAtAsync(string key, DateTime dateTime)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIREAT, key, dateTime.ToUnixTick().ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIREAT, cmd));
        }

        /// <summary>
        /// 将 EXPIREAT 命令加入批量队列，在指定 Unix 秒级时间戳过期
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="timestamp">过期时间，Unix 秒级时间戳</param>
        public void ExpireAtAsync(string key, int timestamp)
        {
            var cmd = _redisCode.Coder(RequestType.EXPIREAT, key, timestamp.ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIREAT, cmd));
        }

        /// <summary>
        /// 将 GEOADD 命令加入批量队列，添加一个或多个地理位置成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="items">待添加的地理位置成员</param>
        public void GeoAddAsync(string key, params GeoItem[] items)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(GeoItem.ToParams(items));

            var cmd = _redisCode.Coder(RequestType.GEOADD, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.GEOADD, cmd));
        }

        /// <summary>
        /// 将 GEODIST 命令加入批量队列，计算两个成员间的距离
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="member1">第一个成员</param>
        /// <param name="member2">第二个成员</param>
        /// <param name="geoUnit">距离单位，默认米</param>
        public void GeoDistAsync(string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m)
        {
            var cmd = _redisCode.Coder(RequestType.GEODIST, key, member1, member2, geoUnit.ToString());

            _batchData.Add(new BatchItem(RequestType.GEODIST, cmd));
        }

        /// <summary>
        /// 将 GET 命令加入批量队列，读取键值
        /// </summary>
        /// <param name="key">键名</param>
        public void GetAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.GET, key);

            _batchData.Add(new BatchItem(RequestType.GET, cmd));
        }

        /// <summary>
        /// 将 GETSET 命令加入批量队列，设置新值并返回旧值
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">新值</param>
        public void GetSetAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.GETSET, key, value);

            _batchData.Add(new BatchItem(RequestType.GETSET, cmd));
        }

        /// <summary>
        /// 将 HDEL 命令加入批量队列，删除哈希表中的一个字段
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">待删除的字段名</param>
        public void HDelAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HDEL, hid, key);

            _batchData.Add(new BatchItem(RequestType.HDEL, cmd));
        }

        /// <summary>
        /// 将 HDEL 命令加入批量队列，删除哈希表中的多个字段
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="keys">待删除的字段名集合</param>
        public void HDelAsync(string hid, params string[] keys)
        {
            var list = new List<string>();

            list.Add(hid);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.HDEL, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.HDEL, cmd));
        }

        /// <summary>
        /// 将 HEXISTS 命令加入批量队列，判断哈希表中字段是否存在
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        public void HExistsAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HEXISTS, hid, key);

            _batchData.Add(new BatchItem(RequestType.HEXISTS, cmd));
        }

        /// <summary>
        /// 将 HGET 命令加入批量队列，读取哈希表中字段的值
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        public void HGetAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HGET, hid, key);

            _batchData.Add(new BatchItem(RequestType.HGET, cmd));
        }

        /// <summary>
        /// 将 HINCRBY 命令加入批量队列，对哈希表字段值加上指定整数
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        /// <param name="num">增加的整数</param>
        public void HIncrementByAsync(string hid, string key, int num)
        {
            var cmd = _redisCode.Coder(RequestType.HINCRBY, hid, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.HINCRBY, cmd));
        }

        /// <summary>
        /// 将 HINCRBYFLOAT 命令加入批量队列，对哈希表字段值加上指定浮点数
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        /// <param name="num">增加的浮点数</param>
        public void HIncrementByFloatAsync(string hid, string key, float num)
        {
            var cmd = _redisCode.Coder(RequestType.HINCRBYFLOAT, hid, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.HINCRBYFLOAT, cmd));
        }

        /// <summary>
        /// 将 HLEN 命令加入批量队列，读取哈希表字段数量
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        public void HLenAsync(string hid)
        {
            var cmd = _redisCode.Coder(RequestType.HLEN, hid);

            _batchData.Add(new BatchItem(RequestType.HLEN, cmd));
        }

        /// <summary>
        /// 将 HSET 命令加入批量队列，设置哈希表中单个字段的值
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        /// <param name="value">字段值</param>
        public void HSetAsync(string hid, string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.HSET, hid, key, value);

            _batchData.Add(new BatchItem(RequestType.HSET, cmd));
        }

        /// <summary>
        /// 将 HSTRLEN 命令加入批量队列，读取哈希表字段值的字符串长度
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="key">字段名</param>
        public void HStrLenAsync(string hid, string key)
        {
            var cmd = _redisCode.Coder(RequestType.HSTRLEN, hid, key);

            _batchData.Add(new BatchItem(RequestType.HSTRLEN, cmd));
        }

        /// <summary>
        /// 将 INCR 命令加入批量队列，键值加一
        /// </summary>
        /// <param name="key">键名</param>
        public void IncrementAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.INCR, key);

            _batchData.Add(new BatchItem(RequestType.INCR, cmd));
        }

        /// <summary>
        /// 将 INCRBY 命令加入批量队列，键值加上指定整数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="num">增加的整数</param>
        public void IncrementByAsync(string key, int num)
        {
            var cmd = _redisCode.Coder(RequestType.INCRBY, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.INCRBY, cmd));
        }

        /// <summary>
        /// 对键值加上指定浮点数
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="num">增加的浮点数</param>
        public void IncrementByFloatAsync(string key, float num)
        {
            var cmd = _redisCode.Coder(RequestType.INCRBY, key, num.ToString());

            _batchData.Add(new BatchItem(RequestType.INCRBY, cmd));
        }

        /// <summary>
        /// 将 STRLEN 命令加入批量队列，读取字符串值的长度
        /// </summary>
        /// <param name="key">键名</param>
        public void LenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.STRLEN, key);

            _batchData.Add(new BatchItem(RequestType.STRLEN, cmd));
        }

        /// <summary>
        /// 将 LINDEX 命令加入批量队列，按下标读取列表元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="index">元素下标，负数表示从表尾开始计数</param>
        public void LIndexAsync(string key, int index)
        {
            var cmd = _redisCode.Coder(RequestType.LINDEX, key, index.ToString());

            _batchData.Add(new BatchItem(RequestType.LINDEX, cmd));
        }

        /// <summary>
        /// 将 LINSERT 命令加入批量队列，在基准元素前后插入新元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="pivot">基准元素</param>
        /// <param name="isBefore">为 true 插在基准元素之前，否则插在其后</param>
        /// <param name="value">待插入的元素</param>
        public void LInsertAsync(string key, string pivot, bool isBefore, string value)
        {
            var beforStr = isBefore ? "BEFORE" : "AFTER";
            var list = new List<string>();
            list.Add(key);
            list.Add(beforStr);
            list.Add(pivot);
            list.Add(value);

            var cmd = _redisCode.Coder(RequestType.LINSERT, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.LINSERT, cmd));
        }

        /// <summary>
        /// 将 LLEN 命令加入批量队列，读取列表长度
        /// </summary>
        /// <param name="key">键名</param>
        public void LLenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.LLEN, key);

            _batchData.Add(new BatchItem(RequestType.LLEN, cmd));
        }

        /// <summary>
        /// 将 LPOP 命令加入批量队列，移除并返回列表头元素
        /// </summary>
        /// <param name="key">键名</param>
        public void LPopAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.LPOP, key);

            _batchData.Add(new BatchItem(RequestType.LPOP, cmd));
        }

        /// <summary>
        /// 将 LPUSH 命令加入批量队列，把多个元素依次插入列表头部
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="lists">待插入的元素集合，按集合顺序入栈</param>
        public void LPushAsync(string key, List<string> lists)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(lists);

            var cmd = _redisCode.Coder(RequestType.LPUSH, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.LPUSH, cmd));
        }

        /// <summary>
        /// 将 LPUSH 命令加入批量队列，把单个元素插入列表头部
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">待插入的元素</param>
        public void LPushAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LPUSH, key, value);

            _batchData.Add(new BatchItem(RequestType.LPUSH, cmd));
        }

        /// <summary>
        /// 将 LPUSHX 命令加入批量队列，仅当列表存在时把元素插入头部
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">待插入的元素</param>
        public void LPushXAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LPUSHX, key, value);

            _batchData.Add(new BatchItem(RequestType.LPUSHX, cmd));
        }

        /// <summary>
        /// 将 LREM 命令加入批量队列，按数量从列表中移除指定元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="count">移除数量，大于 0 从头到尾移除，小于 0 从尾到头移除，等于 0 移除全部</param>
        /// <param name="value">待移除的元素</param>
        public void LRemoveAsync(string key, int count, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LREM, key, count.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.LREM, cmd));
        }

        /// <summary>
        /// 将 LSET 命令加入批量队列，按下标设置列表元素
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="index">元素下标，负数表示从表尾开始计数</param>
        /// <param name="value">新的元素值</param>
        public void LSetAsync(string key, int index, string value)
        {
            var cmd = _redisCode.Coder(RequestType.LSET, key, index.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.LSET, cmd));
        }

        /// <summary>
        /// 将 LTRIM 命令加入批量队列，按区间裁剪列表
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="begin">起始下标，默认 0</param>
        /// <param name="end">结束下标，含该位置，默认 -1 表示表尾</param>
        public void LTrimAsync(string key, int begin = 0, int end = -1)
        {
            var cmd = _redisCode.Coder(RequestType.LTRIM, key, begin.ToString(), end.ToString());

            _batchData.Add(new BatchItem(RequestType.LTRIM, cmd));
        }

        /// <summary>
        /// 移除给定 key 的生存时间，将这个 key 从『易失的』(带生存时间 key )转换成『持久的』(一个不带生存时间、永不过期的 key )。
        /// </summary>
        /// <param name="key"></param>
        public void PersistAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.PERSIST, key);

            _batchData.Add(new BatchItem(RequestType.PERSIST, cmd));
        }

        /// <summary>
        /// 这个命令类似于 TTL 命令，但它以毫秒为单位返回 key 的剩余生存时间，而不是像 TTL 命令那样，以秒为单位。
        /// </summary>
        /// <param name="key"></param>
        public void PttlAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.PTTL, key);

            _batchData.Add(new BatchItem(RequestType.PTTL, cmd));
        }

        /// <summary>
        /// 从当前数据库中随机返回(不删除)一个 key 。
        /// </summary>
        public void RandomKeyAsync()
        {
            var cmd = _redisCode.CodeOnlyParams(RequestType.RANDOMKEY);

            _batchData.Add(new BatchItem(RequestType.RANDOMKEY, cmd));
        }

        /// <summary>
        /// 将 key 改名为 newkey 。
        /// 当 key 和 newkey 相同，或者 key 不存在时，返回一个错误。
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        public void RenameAsync(string oldKey, string newKey)
        {
            var cmd = _redisCode.Coder(RequestType.RENAME, oldKey, newKey);

            _batchData.Add(new BatchItem(RequestType.RENAME, cmd));
        }

        /// <summary>
        /// 移除并返回列表 key 的尾元素
        /// </summary>
        /// <param name="key"></param>
        public void RPopAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.RPOP, key);

            _batchData.Add(new BatchItem(RequestType.RPOP, cmd));
        }

        /// <summary>
        /// 将 RPOPLPUSH 命令加入批量队列，把源列表尾元素移到目标列表头部
        /// </summary>
        /// <param name="source">源列表键名</param>
        /// <param name="destination">目标列表键名</param>
        public void RpopLPushAsync(string source, string destination)
        {
            var cmd = _redisCode.Coder(RequestType.RPOPLPUSH, source, destination);

            _batchData.Add(new BatchItem(RequestType.RPOPLPUSH, cmd));
        }

        /// <summary>
        /// 将一个或多个值 value 插入到列表 key 的表尾(最右边)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void RPushAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.RPUSH, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.RPUSH, cmd));
        }

        /// <summary>
        /// 将值 value 插入到列表 key 的表尾，当且仅当 key 存在并且是一个列表。        
        /// 和 RPUSH 命令相反，当 key 不存在时， RPUSHX 命令什么也不做。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void RPushXAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.RPUSHX, key, value);

            _batchData.Add(new BatchItem(RequestType.RPUSHX, cmd));
        }

        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void SAddAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.SADD, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SADD, cmd));
        }

        /// <summary>
        /// 这个命令的作用和 SDIFF 类似，但它将结果保存到 destination 集合，而不是简单地返回结果集。        
        /// 如果 destination 集合已经存在，则将其覆盖。
        /// destination 可以是 key 本身。
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        public void SDiffStoreAsync(string destination, params string[] keys)
        {
            var list = new List<string>();

            list.Add(destination);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.SDIFFSTORE, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SDIFFSTORE, cmd));
        }

        /// <summary>
        /// 依次加入 SET 与 EXPIRE 命令，写入键值并设置过期时间
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">键值</param>
        /// <param name="seconds">过期时间，单位秒</param>
        public void SetAsync(string key, string value, int seconds)
        {
            var cmd = _redisCode.Coder(RequestType.SET, key, value);

            _batchData.Add(new BatchItem(RequestType.SET, cmd));

            cmd = _redisCode.Coder(RequestType.EXPIRE, key, seconds.ToString());

            _batchData.Add(new BatchItem(RequestType.EXPIRE, cmd));
        }

        /// <summary>
        /// 将 SET 命令加入批量队列，写入键值且不设过期时间
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">键值</param>
        public void SetAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.SET, key, value);

            _batchData.Add(new BatchItem(RequestType.SET, cmd));
        }

        /// <summary>
        /// 判断 member 元素是否集合 key 的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SExistsAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.SISMEMBER, key, value);

            _batchData.Add(new BatchItem(RequestType.SISMEMBER, cmd));
        }

        /// <summary>
        /// 返回集合交集数量并保存到 destination 集合
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        public void SInterStoreAsync(string destination, params string[] keys)
        {
            var list = new List<string>();

            list.Add(destination);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.SINTERSTORE, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SINTERSTORE, cmd));
        }

        /// <summary>
        /// 返回集合 key 的基数(集合中元素的数量)。
        /// </summary>
        /// <param name="key"></param>
        public void SLenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.SCARD, key);

            _batchData.Add(new BatchItem(RequestType.SCARD, cmd));
        }

        /// <summary>
        /// 将 SPOP 命令加入批量队列，随机移除并返回集合中一个元素
        /// </summary>
        /// <param name="key">键名</param>
        public void SPopAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.SPOP, key);

            _batchData.Add(new BatchItem(RequestType.SPOP, cmd));
        }

        /// <summary>
        /// 将 SRANDMEMBER 命令加入批量队列，随机返回集合中一个元素且不删除
        /// </summary>
        /// <param name="key">键名</param>
        public void SRandMemeberAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.SRANDMEMBER, key);

            _batchData.Add(new BatchItem(RequestType.SRANDMEMBER, cmd));
        }

        /// <summary>
        /// 将 SREM 命令加入批量队列，从集合中移除一个或多个成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="values">待移除的成员集合</param>
        public void SRemoveAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.SREM, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SREM, cmd));
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的并集。并将集合保存到destination中
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        public void SUnionStoreAsync(string destination, params string[] keys)
        {
            var list = new List<string>();

            list.Add(destination);

            list.AddRange(keys);

            var cmd = _redisCode.Coder(RequestType.SUNIONSTORE, list.ToArray());

            _batchData.Add(new BatchItem(RequestType.SUNIONSTORE, cmd));
        }

        /// <summary>
        /// 返回生命周期
        /// </summary>
        /// <param name="key"></param>
        public void TtlAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.TTL, key);

            _batchData.Add(new BatchItem(RequestType.TTL, cmd));
        }

        /// <summary>
        /// 将 ZADD 命令加入批量队列，按分值批量添加有序集成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="scoreVals">分值到成员名的映射，同一分值只保留一个成员</param>
        public void ZAddAsync(string key, Dictionary<double, string> scoreVals)
        {
            var cmd = _redisCode.CodeForDicWidthID(RequestType.ZADD, key, scoreVals);

            _batchData.Add(new BatchItem(RequestType.ZADD, cmd));

        }

        /// <summary>
        /// 将 ZADD 命令加入批量队列，添加单个有序集成员
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">成员名</param>
        /// <param name="score">成员分值</param>
        public void ZAddAsync(string key, string value, double score)
        {
            var cmd = _redisCode.Coder(RequestType.ZADD, key, score.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.ZADD, cmd));

        }

        /// <summary>
        /// 将 ZCOUNT 命令加入批量队列，统计分值区间内的成员数量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="begin">分值区间下界，含边界，默认 int 最小值</param>
        /// <param name="end">分值区间上界，含边界，默认 int 最大值</param>
        public void ZCountAsync(string key, double begin = -2147483648, double end = 2147483647)
        {
            var cmd = _redisCode.Coder(RequestType.ZCOUNT, key, begin.ToString(), end.ToString());

            _batchData.Add(new BatchItem(RequestType.ZCOUNT, cmd));
        }

        /// <summary>
        /// 将 ZINCRBY 命令加入批量队列，对指定成员的分值加上浮点增量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="increment">分值增量</param>
        /// <param name="value">成员名</param>
        public void ZIncrByAsync(string key, double increment, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZINCRBY, key, increment.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.ZINCRBY, cmd));
        }

        /// <summary>
        /// 将 ZINCRBY 命令加入批量队列，对指定成员的分值加上整数增量
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="increment">分值增量</param>
        /// <param name="value">成员名</param>
        public void ZIncrByAsync(string key, long increment, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZINCRBY, key, increment.ToString(), value);

            _batchData.Add(new BatchItem(RequestType.ZINCRBY, cmd));
        }

        /// <summary>
        /// 将 ZCARD 命令加入批量队列，读取有序集的成员数量
        /// </summary>
        /// <param name="key">键名</param>
        public void ZLenAsync(string key)
        {
            var cmd = _redisCode.Coder(RequestType.ZCARD, key);

            _batchData.Add(new BatchItem(RequestType.ZCARD, cmd));
        }

        /// <summary>
        /// 将 ZLEXCOUNT 命令加入批量队列，统计字典序区间内的成员数量，offset 与 count 一并作为命令参数发送
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="min">字典序下界，默认 double 最小值</param>
        /// <param name="max">字典序上界，默认 double 最大值</param>
        /// <param name="offset">偏移量，默认 -1</param>
        /// <param name="count">统计数量上限，默认 20</param>
        public void ZLexCountAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            var cmd = _redisCode.Coder(RequestType.ZLEXCOUNT, key, min.ToString(), max.ToString(), offset.ToString(), count.ToString());

            _batchData.Add(new BatchItem(RequestType.ZLEXCOUNT, cmd));
        }

        /// <summary>
        /// 返回有序集 key 中成员 member 的排名
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ZRankAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZRANK, key, value);

            _batchData.Add(new BatchItem(RequestType.ZRANK, cmd));
        }
        /// <summary>
        /// 移除有序集 key 中的一个或多个成员，不存在的成员将被忽略。
        /// 当 key 存在但不是有序集类型时，返回一个错误
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void ZRemoveAsync(string key, params string[] values)
        {
            var list = new List<string>();

            list.Add(key);

            list.AddRange(values);

            var cmd = _redisCode.Coder(RequestType.ZREM, list.ToString());

            _batchData.Add(new BatchItem(RequestType.ZREM, cmd));
        }

        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void ZRemoveByLexAsync(string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20)
        {
            var cmd = _redisCode.CodeForRandByScore(RequestType.ZREMRANGEBYLEX, key, min, max, RangType.None, offset, count);

            _batchData.Add(new BatchItem(RequestType.ZREMRANGEBYLEX, cmd));
        }

        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void ZRemoveByRankAsync(string key, double start = 0, double stop = -1)
        {
            var cmd = _redisCode.CodeForRandByScore(RequestType.ZREMRANGEBYRANK, key, start, stop, RangType.None, -1, 20);

            _batchData.Add(new BatchItem(RequestType.ZREMRANGEBYRANK, cmd));
        }

        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="rangType"></param>
        public void ZRemoveByScoreAsync(string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None)
        {
            var cmd = _redisCode.CodeForRandByScore(RequestType.ZREMRANGEBYSCORE, key, min, max, RangType.None, -1, 20);

            _batchData.Add(new BatchItem(RequestType.ZREMRANGEBYSCORE, cmd));
        }

        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。成员按 score 值递减
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ZRevRankAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZREVRANGE, key, value);

            _batchData.Add(new BatchItem(RequestType.ZREVRANGE, cmd));
        }

        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ZScoreAsync(string key, string value)
        {
            var cmd = _redisCode.Coder(RequestType.ZSCORE, key, value);

            _batchData.Add(new BatchItem(RequestType.ZSCORE, cmd));
        }

        /// <summary>
        /// 释放批量队列，清空已缓存的命令
        /// </summary>
        public void Dispose()
        {
            if (_batchData != null)
            {
                _batchData.Clear();
            }
        }
    }
}
