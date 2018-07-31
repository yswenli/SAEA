/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RequestType
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

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

        SET,
        GET,
        DEL,
        KEYS,
        EXISTS,
        EXPIRE,
        PERSIST,
        GETSET,
        SETNX,
        RENAME,

        HSET,
        HGET,
        HGETALL,
        HKEYS,
        HLEN,
        HDEL,
        HEXISTS,

        LSET,
        LLEN,
        LPUSH,
        LPOP,
        RPUSH,
        RPOP,
        LRANGE,
        LREM,

        SADD,
        SCARD,
        SISMEMBER,
        SMEMBERS,
        SRANDMEMBER,
        SPOP,
        SREM,

        ZADD,
        ZCARD,
        ZCOUNT,
        ZRANGE,
        ZREVRANGE,
        ZREM,

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

    }
}
