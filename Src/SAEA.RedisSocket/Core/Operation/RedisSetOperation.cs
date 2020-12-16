/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisSetOperation
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/15 15:45:14
*描述：
*=====================================================================
*修改时间：2019/8/15 15:45:14
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.RedisSocket.Model;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// set
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SAdd(string key, string value)
        {
            RedisConnection.DoWithKeyValue(RequestType.SADD, key, value);
        }

        /// <summary>
        /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SAdd(string key, string[] value)
        {
            RedisConnection.DoBatchWithIDKeys(RequestType.SADD, key, value);
        }

        /// <summary>
        /// 判断 member 元素是否集合 key 的成员
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SExists(string key, string value)
        {
            var result = RedisConnection.DoWithKeyValue(RequestType.SISMEMBER, key, value).Data;
            return result.IndexOf("1") > -1 ? true : false;
        }

        /// <summary>
        /// 移除并返回集合中的一个随机元素
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string SPop(string key)
        {
            return RedisConnection.DoWithKey(RequestType.SPOP, key).Data;
        }

        /// <summary>
        /// 返回集合中的一个随机元素
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string SRandMemeber(string key)
        {
            return RedisConnection.DoWithKey(RequestType.SRANDMEMBER, key).Data;
        }

        /// <summary>
        /// 移除集合 key 中的一个或多个 member 元素
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public int SRemove(string key, params string[] values)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoBatchWithIDKeys(RequestType.SREM, key, values).Data, out result);
            return result;
        }

        /// <summary>
        /// 将 member 元素从 source 集合移动到 destination 集合
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public int SMove(string source, string destination, string key)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoWithID(RequestType.SMOVE, source, destination, key).Data, out result);
            return result;
        }

        /// <summary>
        /// 回集合 key 的基数(集合中元素的数量)
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int SLen(string key)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoWithKey(RequestType.SCARD, key).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回集合 key 中的所有成员
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<string> SMemebers(string key)
        {
            return RedisConnection.DoWithKey(RequestType.SMEMBERS, key).ToList();
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的交集
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public List<string> SInter(params string[] keys)
        {
            return RedisConnection.DoWithMutiParams(RequestType.SINTER, keys).ToList();
        }

        /// <summary>
        /// 类似于 SINTER key [key …] 命令，但它将结果保存到 destination 集合，而不是简单地返回结果集
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <returns>结果集中的成员数量</returns>
        public int SInterStore(string destination, params string[] keys)
        {
            keys.NotNull();
            var result = 0;
            int.TryParse(RedisConnection.DoMultiLineWithList(RequestType.SINTERSTORE, destination, keys.ToList()).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合的并集
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public List<string> SUnion(params string[] keys)
        {
            return RedisConnection.DoWithMutiParams(RequestType.SUNION, keys).ToList();
        }

        /// <summary>
        /// 类似于 SUNION key [key …] 命令，但它将结果保存到 destination 集合，而不是简单地返回结果集
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <returns>结果集中的成员数量</returns>
        public int SUnionStore(string destination, params string[] keys)
        {
            keys.NotNull();
            var result = 0;
            int.TryParse(RedisConnection.DoMultiLineWithList(RequestType.SUNIONSTORE, destination, keys.ToList()).Data, out result);
            return result;
        }

        /// <summary>
        /// 返回一个集合的全部成员，该集合是所有给定集合之间的差集
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public List<string> SDiff(params string[] keys)
        {
            return RedisConnection.DoWithMutiParams(RequestType.SDIFF, keys).ToList();
        }

        /// <summary>
        /// 类似于 SDIFF key [key …] 命令，但它将结果保存到 destination 集合，而不是简单地返回结果集
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="keys"></param>
        /// <returns>结果集中的成员数量</returns>
        public int SDiffStore(string destination, params string[] keys)
        {
            keys.NotNull();
            var result = 0;
            int.TryParse(RedisConnection.DoMultiLineWithList(RequestType.SDIFFSTORE, destination, keys.ToList()).Data, out result);
            return result;
        }

    }
}
