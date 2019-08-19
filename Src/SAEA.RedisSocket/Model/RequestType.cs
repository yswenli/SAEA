/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket
*文件名： RequestType
*版本号： v4.5.6.7
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
*版本号： v4.5.6.7
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
        TTL,
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
        SUNIONSTORE,
        SDIFF,
        SDIFFSTORE,

        ZADD,
        ZSCORE,
        ZINCRBY,
        ZCARD,
        ZCOUNT,
        ZRANGE,
        ZREVRANGE,
        ZRANGEBYSCORE,
        ZREVRANGEBYSCORE,
        ZRANK,
        ZREVRANK,
        ZREM,
        ZREMRANGEBYRANK,
        ZREMRANGEBYSCORE,
        ZRANGEBYLEX,
        ZLEXCOUNT,
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
