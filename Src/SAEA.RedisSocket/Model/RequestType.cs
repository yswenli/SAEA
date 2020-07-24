/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket
*文件名： RequestType
*版本号： v5.0.0.1
*唯一标识：5b3016dd-47f6-4cc4-a27b-9334c79294b8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 15:13:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 15:13:39
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
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

    }
}
