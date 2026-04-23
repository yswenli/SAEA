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
*命名空间：SAEA.RedisSocket.Model
*文件名： RequestType
*版本号： v26.4.23.1
*唯一标识：407d40be-062b-4dae-8a33-93534286a21a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/20 10:04:58
*描述：RequestType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2018/03/20 10:04:58
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RequestType类型枚举
*
*****************************************************************************/
namespace SAEA.RedisSocket.Model
{
    public enum RequestType
    {
        AUTH,
        PING,
        SELECT,
        DBSIZE,
        TYPE,
        INFO,
        SLAVEOF,
        FLUSHALL,
        FLUSHDB,
        /// <summary>
        /// 返回生命周期,秒
        /// </summary>
        TTL,
        /// <summary>
        /// 返回生命周期,毫秒
        /// </summary>
        PTTL,
        RANDOMKEY,

        SET,
        APPEND,
        GETSET,
        MSET,
        MSETNX,
        GET,
        MGET,
        DEL,
        KEYS,
        EXISTS,
        EXPIRE,
        EXPIREAT,
        PERSIST,
        SETNX,
        RENAME,
        INCR,
        DECR,
        INCRBY,
        DECRBY,
        INCRBYFLOAT,
        STRLEN,


        HSET,
        HMSET,
        HGET,
        HMGET,
        HGETALL,
        HKEYS,
        HVALS,
        HLEN,
        HDEL,
        HEXISTS,
        HSTRLEN,
        HINCRBY,
        HINCRBYFLOAT,


        LSET,
        LLEN,
        LINDEX,
        LINSERT,
        LPUSH,
        LPUSHX,
        LPOP,
        RPUSH,
        RPUSHX,
        RPOP,
        RPOPLPUSH,
        LRANGE,
        LTRIM,
        LREM,
        BLPOP,
        BRPOP,
        BRPOPLPUSH,


        SADD,
        /// <summary>
        /// 返回集合 key 的基数(集合中元素的数量)。
        /// </summary>
        SCARD,
        SISMEMBER,
        SMEMBERS,
        SRANDMEMBER,
        SPOP,
        SREM,
        SMOVE,
        SINTER,
        /// <summary>
        /// 返回集合交集数量并保存到 destination 集合
        /// </summary>
        SINTERSTORE,
        SUNION,
        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的并集。并将集合保存到destination中
        /// </summary>
        SUNIONSTORE,
        SDIFF,
        SDIFFSTORE,

        ZADD,
        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值。
        /// </summary>
        ZSCORE,
        ZINCRBY,
        /// <summary>
        /// 返回有序集 key 的基数
        /// </summary>
        ZCARD,
        ZCOUNT,
        ZRANGE,
        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。成员按 score 值递减
        /// </summary>
        ZREVRANGE,
        ZRANGEBYSCORE,
        ZREVRANGEBYSCORE,
        /// <summary>
        /// 返回有序集 key 中成员 member 的排名
        /// </summary>
        ZRANK,
        ZREVRANK,
        ZREM,
        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        ZREMRANGEBYRANK,
        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        ZREMRANGEBYSCORE,
        ZRANGEBYLEX,
        ZLEXCOUNT,
        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// </summary>
        ZREMRANGEBYLEX,


        GEOADD,
        GEOPOS,
        GEODIST,
        GEORADIUS,
        GEORADIUSBYMEMBER,

        PUBLISH,
        SUBSCRIBE,
        UNSUBSCRIBE,

        SCAN,
        HSCAN,
        SSCAN,
        ZSCAN,

        CLUSTER_INFO,
        CLUSTER_NODES,
        CLUSTER_MEET,
        CLUSTER_FORGET,
        CLUSTER_FAILOVER,
        CLUSTER_REPLICATE,
        CLUSTER_SAVECONFIG,
        CLUSTER_ADDSLOTS,
        CLUSTER_DELSLOTS,
        CLUSTER_FLUSHSLOTS,
        CLUSTER_SETSLOT,
        CLUSTER_KEYSLOT,
        CLUSTER_COUNTKEYSINSLOT,
        CLUSTER_GETKEYSINSLOT,

        CONFIG_GET,
        CONFIG_SET,

        CLIENT_LIST,

        /// <summary>
        /// 用于向 stream 添加消息，如果指定的 stream 不存在，则创建一个 stream
        /// </summary>
        XADD,
        /// <summary>
        /// 管理流数据结构关联的消费者组
        /// </summary>
        XGROUP,

        /// <summary>
        /// 从一个或多个流中读取数据，只返回ID大于调用者报告的上一个接收ID的条目。
        /// </summary>
        XREAD,
        /// <summary>
        /// 从一个或多个流中读取数据，只返回ID大于调用者报告的上一个接收ID的条目。
        /// </summary>
        XREADGROUP,
        /// <summary>
        /// 确认消息
        /// </summary>
        XACK,
        XLEN,
        /// <summary>
        /// 返回范围内的消息信息
        /// </summary>
        XRANGE


    }
}