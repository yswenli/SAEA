/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisStringOperation
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/14 15:23:47
*描述：
*=====================================================================
*修改时间：2019/8/14 15:23:47
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// string 操作
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将字符串值 value 关联到 key 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, string value)
        {
            RedisConnection.DoWithKeyValue(RequestType.SET, key, value);
        }

        /// <summary>
        /// 将字符串值 value 关联到 key 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="seconds"></param>
        public void Set(string key, string value, int seconds)
        {
            RedisConnection.DoExpireInsert(RequestType.SET, key, value, seconds);
        }

        /// <summary>
        /// 将字符串值 value 关联到 key
        /// </summary>
        /// <param name="dic"></param>
        public void MSet(Dictionary<string, string> dic)
        {
            RedisConnection.DoMultiLineWithDic(RequestType.MSET, dic);
        }

        /// <summary>
        /// 当且仅当所有给定键都不存在时， 为所有给定键设置值
        /// </summary>
        /// <param name="dic"></param>
        public void MSetNx(Dictionary<string, string> dic)
        {
            RedisConnection.DoMultiLineWithDic(RequestType.MSETNX, dic);
        }

        /// <summary>
        /// 如果键 key 已经存在并且它的值是一个字符串， APPEND 命令将把 value 追加到键 key 现有值的末尾
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public int Append(string key, string value)
        {
            return int.Parse(RedisConnection.DoWithKeyValue(RequestType.APPEND, key, value).Data);
        }

        /// <summary>
        /// 返回 key 所关联的字符串值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            return RedisConnection.DoWithKey(RequestType.GET, key).Data;
        }

        /// <summary>
        /// 返回 key 所关联的字符串值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public List<string> MGet(params string[] keys)
        {
            return RedisConnection.DoWithMutiParams(RequestType.MGET, keys).ToList();
        }

        /// <summary>
        /// 如果 key 已经持有其他值， SET 就覆写旧值，无视类型
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public string GetSet(string key, string value)
        {
            return RedisConnection.DoWithKeyValue(RequestType.GETSET, key, value).Data;
        }

        /// <summary>
        /// 为键 key 储存的数字值加上一
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long Increment(string key)
        {
            return long.Parse(RedisConnection.DoWithKey(RequestType.INCR, key).Data);
        }

        /// <summary>
        /// 为键 key 储存的数字值减去一
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long Decrement(string key)
        {
            return long.Parse(RedisConnection.DoWithKey(RequestType.DECR, key).Data);
        }

        /// <summary>
        /// 为键 key 储存的数字值加上增量 increment 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public long IncrementBy(string key, int num)
        {
            return long.Parse(RedisConnection.DoWithKeyValue(RequestType.INCRBY, key, num.ToString()).Data);
        }

        /// <summary>
        /// 为键 key 储存的数字值加上增量 increment 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public long DecrementBy(string key, int num)
        {
            return long.Parse(RedisConnection.DoWithKeyValue(RequestType.DECRBY, key, num.ToString()).Data);
        }

        /// <summary>
        /// 为键 key 储存的值加上浮点数增量 increment
        /// </summary>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public float IncrementByFloat(string key, float num)
        {
            return float.Parse(RedisConnection.DoWithKeyValue(RequestType.INCRBYFLOAT, key, num.ToString()).Data);
        }

        /// <summary>
        /// 返回键 key 储存的字符串值的长度
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int Len(string key)
        {
            return int.Parse(RedisConnection.DoWithKey(RequestType.STRLEN, key).Data);
        }
    }
}
