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
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis 命令名，枚举文本会直接作为协议中的命令下发
    /// </summary>
    public enum RequestType
    {
        /// <summary>
        /// 使用密码认证
        /// </summary>
        AUTH,
        /// <summary>
        /// 探测连接是否可用
        /// </summary>
        PING,
        /// <summary>
        /// 切换数据库
        /// </summary>
        SELECT,
        /// <summary>
        /// 返回当前数据库的 key 数量
        /// </summary>
        DBSIZE,
        /// <summary>
        /// 返回 key 的数据类型
        /// </summary>
        TYPE,
        /// <summary>
        /// 返回服务器信息与统计
        /// </summary>
        INFO,
        /// <summary>
        /// 设置或取消从节点跟随
        /// </summary>
        SLAVEOF,
        /// <summary>
        /// 清空所有数据库
        /// </summary>
        FLUSHALL,
        /// <summary>
        /// 清空当前数据库
        /// </summary>
        FLUSHDB,
        /// <summary>
        /// 返回生命周期,秒
        /// </summary>
        TTL,
        /// <summary>
        /// 返回生命周期,毫秒
        /// </summary>
        PTTL,
        /// <summary>
        /// 随机返回一个 key
        /// </summary>
        RANDOMKEY,

        /// <summary>
        /// 写入字符串值
        /// </summary>
        SET,
        /// <summary>
        /// 在字符串末尾追加内容
        /// </summary>
        APPEND,
        /// <summary>
        /// 写入新值并返回旧值
        /// </summary>
        GETSET,
        /// <summary>
        /// 批量写入多个键值
        /// </summary>
        MSET,
        /// <summary>
        /// 所有键都不存在时才批量写入
        /// </summary>
        MSETNX,
        /// <summary>
        /// 读取字符串值
        /// </summary>
        GET,
        /// <summary>
        /// 批量读取多个 key 的值
        /// </summary>
        MGET,
        /// <summary>
        /// 删除 key
        /// </summary>
        DEL,
        /// <summary>
        /// 按模式匹配查找 key
        /// </summary>
        KEYS,
        /// <summary>
        /// 判断 key 是否存在
        /// </summary>
        EXISTS,
        /// <summary>
        /// 设置 key 的过期时间，单位秒
        /// </summary>
        EXPIRE,
        /// <summary>
        /// 设置 key 的过期时间戳，单位秒
        /// </summary>
        EXPIREAT,
        /// <summary>
        /// 移除 key 的过期时间
        /// </summary>
        PERSIST,
        /// <summary>
        /// key 不存在时写入值
        /// </summary>
        SETNX,
        /// <summary>
        /// 重命名 key
        /// </summary>
        RENAME,
        /// <summary>
        /// 整数值加一
        /// </summary>
        INCR,
        /// <summary>
        /// 整数值减一
        /// </summary>
        DECR,
        /// <summary>
        /// 整数值增加指定步长
        /// </summary>
        INCRBY,
        /// <summary>
        /// 整数值减少指定步长
        /// </summary>
        DECRBY,
        /// <summary>
        /// 浮点数值增加指定步长
        /// </summary>
        INCRBYFLOAT,
        /// <summary>
        /// 返回字符串值的字节长度
        /// </summary>
        STRLEN,


        /// <summary>
        /// 设置哈希表中单个字段的值
        /// </summary>
        HSET,
        /// <summary>
        /// 批量设置哈希表中的多个字段值
        /// </summary>
        HMSET,
        /// <summary>
        /// 读取哈希表中单个字段的值
        /// </summary>
        HGET,
        /// <summary>
        /// 批量读取哈希表中多个字段的值
        /// </summary>
        HMGET,
        /// <summary>
        /// 返回哈希表的全部字段与值
        /// </summary>
        HGETALL,
        /// <summary>
        /// 返回哈希表的全部字段名
        /// </summary>
        HKEYS,
        /// <summary>
        /// 返回哈希表的全部字段值
        /// </summary>
        HVALS,
        /// <summary>
        /// 返回哈希表的字段数量
        /// </summary>
        HLEN,
        /// <summary>
        /// 删除哈希表中的字段
        /// </summary>
        HDEL,
        /// <summary>
        /// 判断哈希表中字段是否存在
        /// </summary>
        HEXISTS,
        /// <summary>
        /// 返回哈希表字段值的字节长度
        /// </summary>
        HSTRLEN,
        /// <summary>
        /// 哈希表字段的整数值增加指定步长
        /// </summary>
        HINCRBY,
        /// <summary>
        /// 哈希表字段的浮点数值增加指定步长
        /// </summary>
        HINCRBYFLOAT,


        /// <summary>
        /// 按下标设置列表元素的值
        /// </summary>
        LSET,
        /// <summary>
        /// 返回列表长度
        /// </summary>
        LLEN,
        /// <summary>
        /// 按下标读取列表元素
        /// </summary>
        LINDEX,
        /// <summary>
        /// 在列表指定元素前后插入新元素
        /// </summary>
        LINSERT,
        /// <summary>
        /// 从列表左侧插入元素
        /// </summary>
        LPUSH,
        /// <summary>
        /// 列表存在时才从左侧插入元素
        /// </summary>
        LPUSHX,
        /// <summary>
        /// 从列表左侧弹出元素
        /// </summary>
        LPOP,
        /// <summary>
        /// 从列表右侧插入元素
        /// </summary>
        RPUSH,
        /// <summary>
        /// 列表存在时才从右侧插入元素
        /// </summary>
        RPUSHX,
        /// <summary>
        /// 从列表右侧弹出元素
        /// </summary>
        RPOP,
        /// <summary>
        /// 弹出右侧元素并推入另一列表左侧
        /// </summary>
        RPOPLPUSH,
        /// <summary>
        /// 按区间读取列表元素
        /// </summary>
        LRANGE,
        /// <summary>
        /// 裁剪列表，只保留指定区间内的元素
        /// </summary>
        LTRIM,
        /// <summary>
        /// 按值删除列表元素
        /// </summary>
        LREM,
        /// <summary>
        /// 从列表左侧阻塞弹出元素
        /// </summary>
        BLPOP,
        /// <summary>
        /// 从列表右侧阻塞弹出元素
        /// </summary>
        BRPOP,
        /// <summary>
        /// 阻塞版弹出并推入另一列表
        /// </summary>
        BRPOPLPUSH,


        /// <summary>
        /// 向集合添加成员
        /// </summary>
        SADD,
        /// <summary>
        /// 返回集合 key 的基数(集合中元素的数量)。
        /// </summary>
        SCARD,
        /// <summary>
        /// 判断成员是否属于集合
        /// </summary>
        SISMEMBER,
        /// <summary>
        /// 返回集合的全部成员
        /// </summary>
        SMEMBERS,
        /// <summary>
        /// 随机返回集合中的成员
        /// </summary>
        SRANDMEMBER,
        /// <summary>
        /// 随机弹出并返回集合中的成员
        /// </summary>
        SPOP,
        /// <summary>
        /// 从集合中移除成员
        /// </summary>
        SREM,
        /// <summary>
        /// 把成员从一个集合移动到另一个集合
        /// </summary>
        SMOVE,
        /// <summary>
        /// 返回多个集合的交集
        /// </summary>
        SINTER,
        /// <summary>
        /// 返回集合交集数量并保存到 destination 集合
        /// </summary>
        SINTERSTORE,
        /// <summary>
        /// 返回多个集合的并集
        /// </summary>
        SUNION,
        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的并集。并将集合保存到destination中
        /// </summary>
        SUNIONSTORE,
        /// <summary>
        /// 返回多个集合的差集
        /// </summary>
        SDIFF,
        /// <summary>
        /// 计算多个集合的差集并保存到目标集合
        /// </summary>
        SDIFFSTORE,

        /// <summary>
        /// 向有序集合添加成员或更新分值
        /// </summary>
        ZADD,
        /// <summary>
        /// 返回有序集 key 中，成员 member 的 score 值。
        /// </summary>
        ZSCORE,
        /// <summary>
        /// 有序集合成员的分值增加指定增量
        /// </summary>
        ZINCRBY,
        /// <summary>
        /// 返回有序集 key 的基数
        /// </summary>
        ZCARD,
        /// <summary>
        /// 统计分值落在区间内的成员数量
        /// </summary>
        ZCOUNT,
        /// <summary>
        /// 按分值升序返回指定排名区间的成员
        /// </summary>
        ZRANGE,
        /// <summary>
        /// 返回有序集 key 中，指定区间内的成员。成员按 score 值递减
        /// </summary>
        ZREVRANGE,
        /// <summary>
        /// 按分值区间升序返回成员
        /// </summary>
        ZRANGEBYSCORE,
        /// <summary>
        /// 按分值区间降序返回成员
        /// </summary>
        ZREVRANGEBYSCORE,
        /// <summary>
        /// 返回有序集 key 中成员 member 的排名
        /// </summary>
        ZRANK,
        /// <summary>
        /// 按分值降序返回成员排名
        /// </summary>
        ZREVRANK,
        /// <summary>
        /// 从有序集合中移除成员
        /// </summary>
        ZREM,
        /// <summary>
        /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
        /// </summary>
        ZREMRANGEBYRANK,
        /// <summary>
        /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员
        /// </summary>
        ZREMRANGEBYSCORE,
        /// <summary>
        /// 按字典序返回分值相同的成员区间
        /// </summary>
        ZRANGEBYLEX,
        /// <summary>
        /// 统计字典序区间内的成员数量
        /// </summary>
        ZLEXCOUNT,
        /// <summary>
        /// 对于一个所有成员的分值都相同的有序集合键 key 来说， 这个命令会移除该集合中， 成员介于 min 和 max 范围内的所有元素。
        /// </summary>
        ZREMRANGEBYLEX,


        /// <summary>
        /// 添加地理位置成员
        /// </summary>
        GEOADD,
        /// <summary>
        /// 返回成员的经纬度
        /// </summary>
        GEOPOS,
        /// <summary>
        /// 返回两个成员之间的距离
        /// </summary>
        GEODIST,
        /// <summary>
        /// 按圆心与半径查询范围内的成员
        /// </summary>
        GEORADIUS,
        /// <summary>
        /// 以指定成员为中心查询范围内的成员
        /// </summary>
        GEORADIUSBYMEMBER,

        /// <summary>
        /// 向频道发布消息
        /// </summary>
        PUBLISH,
        /// <summary>
        /// 订阅频道
        /// </summary>
        SUBSCRIBE,
        /// <summary>
        /// 取消订阅频道
        /// </summary>
        UNSUBSCRIBE,

        /// <summary>
        /// 游标方式遍历当前库的 key
        /// </summary>
        SCAN,
        /// <summary>
        /// 游标方式遍历哈希表的字段
        /// </summary>
        HSCAN,
        /// <summary>
        /// 游标方式遍历集合的成员
        /// </summary>
        SSCAN,
        /// <summary>
        /// 游标方式遍历有序集合的成员
        /// </summary>
        ZSCAN,

        /// <summary>
        /// 返回集群状态概览
        /// </summary>
        CLUSTER_INFO,
        /// <summary>
        /// 返回集群全部节点
        /// </summary>
        CLUSTER_NODES,
        /// <summary>
        /// 将节点加入集群
        /// </summary>
        CLUSTER_MEET,
        /// <summary>
        /// 从集群中移除节点
        /// </summary>
        CLUSTER_FORGET,
        /// <summary>
        /// 在主节点与其从节点间切换角色
        /// </summary>
        CLUSTER_FAILOVER,
        /// <summary>
        /// 将当前节点设为指定节点从属
        /// </summary>
        CLUSTER_REPLICATE,
        /// <summary>
        /// 保存集群配置到磁盘
        /// </summary>
        CLUSTER_SAVECONFIG,
        /// <summary>
        /// 为当前节点分配槽位
        /// </summary>
        CLUSTER_ADDSLOTS,
        /// <summary>
        /// 从当前节点移除槽位
        /// </summary>
        CLUSTER_DELSLOTS,
        /// <summary>
        /// 清空当前节点的全部槽位
        /// </summary>
        CLUSTER_FLUSHSLOTS,
        /// <summary>
        /// 将槽位指定给某个节点
        /// </summary>
        CLUSTER_SETSLOT,
        /// <summary>
        /// 计算 key 所属的槽位编号
        /// </summary>
        CLUSTER_KEYSLOT,
        /// <summary>
        /// 统计槽位内的 key 数量
        /// </summary>
        CLUSTER_COUNTKEYSINSLOT,
        /// <summary>
        /// 列出槽位内的 key
        /// </summary>
        CLUSTER_GETKEYSINSLOT,

        /// <summary>
        /// 读取配置项
        /// </summary>
        CONFIG_GET,
        /// <summary>
        /// 设置配置项
        /// </summary>
        CONFIG_SET,

        /// <summary>
        /// 列出当前连接的客户端
        /// </summary>
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
        /// <summary>
        /// 返回 stream 中的消息条数
        /// </summary>
        XLEN,
        /// <summary>
        /// 返回范围内的消息信息
        /// </summary>
        XRANGE


    }
}