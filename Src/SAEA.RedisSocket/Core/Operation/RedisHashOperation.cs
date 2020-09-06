/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisHashOperation
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/14 15:53:12
*描述：
*=====================================================================
*修改时间：2019/8/14 15:53:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// Hash操作
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将哈希表 hash 中域 field 的值设置为 value 
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void HSet(string hid, string key, string value)
        {
            RedisConnection.DoWithID(RequestType.HSET, hid, key, value);
        }
        /// <summary>
        /// 同时将多个 field-value (域-值)对设置到哈希表 key 中
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="keyvalues"></param>
        public void HMSet(string hid, Dictionary<string, string> keyvalues)
        {
            RedisConnection.DoBatchWithIDDic(RequestType.HMSET, hid, keyvalues);
        }
        /// <summary>
        /// 返回哈希表中给定域的值
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public string HGet(string hid, string key)
        {
            return RedisConnection.DoWithKeyValue(RequestType.HGET, hid, key).Data;
        }

        /// <summary>
        /// 返回哈希表中给定域的值
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public T HGetObj<T>(string hid, string key)
        {
            return HGet(hid, key).JsonToObj<T>();
        }

        /// <summary>
        /// 返回哈希表中给定域的值
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public T HGet<T>(string hid, string key)
        {
            return HGet(hid, key).JsonToObj<T>();
        }

        /// <summary>
        /// 返回哈希表 key 中，一个或多个给定域的值
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public List<string> HMGet(string hid, params string[] keys)
        {
            return RedisConnection.DoMultiLineWithList(RequestType.HMGET, hid, keys).ToList();
        }

        /// <summary>
        /// 返回哈希表 key 中，一个或多个给定域的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hid"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public List<T> HMGetList<T>(string hid, params string[] keys)
        {
            return HMGet(hid, keys).JsonToObj<T>();
        }
        /// <summary>
        /// 返回哈希表 key 中，所有的域和值
        /// </summary>
        /// <param name="hid"></param>
        /// <returns></returns>
        public Dictionary<string, string> HGetAll(string hid)
        {
            return RedisConnection.DoWithKey(RequestType.HGETALL, hid).ToKeyValues();
        }

        /// <summary>
        /// 返回哈希表 key 中，所有的域和值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hid"></param>
        /// <returns></returns>
        public Dictionary<string, T> HGetAll<T>(string hid)
        {
            Dictionary<string, T> result;

            var dic = HGetAll(hid);

            if (dic == null || !dic.Any()) return null;

            result = new Dictionary<string, T>();

            foreach (var item in dic)
            {
                result.Add(item.Key, item.Value.JsonToObj<T>());
            }

            return result;
        }

        /// <summary>
        /// 返回哈希表 key 中的所有域
        /// </summary>
        /// <param name="hid"></param>
        /// <returns></returns>
        public List<string> HGetKeys(string hid)
        {
            return RedisConnection.DoWithKey(RequestType.HKEYS, hid).ToList();
        }

        /// <summary>
        /// 返回哈希表 key 中所有域的值
        /// </summary>
        /// <param name="hid"></param>
        /// <returns></returns>
        public List<string> HGetValues(string hid)
        {
            return RedisConnection.DoWithKey(RequestType.HVALS, hid).ToList();
        }

        /// <summary>
        /// 返回哈希表 key 中所有域的值
        /// </summary>
        /// <param name="hid"></param>
        /// <returns></returns>
        public List<T> HGetValueList<T>(string hid)
        {
            return HGetValues(hid).JsonToObj<T>();
        }

        /// <summary>
        /// 删除哈希表 key 中的一个或多个指定域，不存在的域将被忽略
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public int HDel(string hid, string key)
        {
            var result = RedisConnection.DoWithKeyValue(RequestType.HDEL, hid, key);

            if (int.TryParse(result.Data, out int count))
            {
                return count;
            }
            return 0;
        }

        /// <summary>
        /// 删除哈希表 key 中的一个或多个指定域，不存在的域将被忽略
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public int HDel(string hid, params string[] keys)
        {
            var result = RedisConnection.DoBatchWithIDKeys(RequestType.HDEL, hid, keys);

            if (int.TryParse(result.Data, out int count))
            {
                return count;
            }
            return 0;
        }

        /// <summary>
        /// 返回哈希表 key 中域的数量
        /// </summary>
        /// <param name="hid"></param>
        /// <returns></returns>
        public int HLen(string hid)
        {
            if (int.TryParse(RedisConnection.DoWithKey(RequestType.HLEN, hid).Data, out int result))
                return result;
            return 0;
        }
        /// <summary>
        /// 检查给定域 field 是否存在于哈希表 hash 当中
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool HExists(string hid, string key)
        {
            var result = RedisConnection.DoWithKeyValue(RequestType.HEXISTS, hid, key).Data;
            return result == "1" ? true : false;
        }

        /// <summary>
        /// 返回哈希表 key 中， 与给定域 field 相关联的值的字符串长度
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public int HStrLen(string hid, string key)
        {
            if (int.TryParse(RedisConnection.DoWithKeyValue(RequestType.HSTRLEN, hid, key).Data, out int result))

                return result;

            return 0;
        }

        /// <summary>
        /// 为哈希表 key 中的域 field 的值加上增量 increment 
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public long HIncrementBy(string hid, string key, int num)
        {
            return long.Parse(RedisConnection.DoWithID(RequestType.HINCRBY, hid, key, num.ToString()).Data);
        }

        /// <summary>
        /// 为哈希表 key 中的域 field 加上浮点数增量 increment
        /// </summary>
        /// <param name="hid"></param>
        /// <param name="key"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public float HIncrementByFloat(string hid, string key, float num)
        {
            return float.Parse(RedisConnection.DoWithID(RequestType.HINCRBYFLOAT, hid, key, num.ToString()).Data);
        }

    }
}
