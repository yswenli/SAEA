/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisID
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/7 17:59:49
*描述：
*=====================================================================
*修改时间：2021/1/7 17:59:49
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// RedisID
    /// </summary>
    public class RedisID
    {
        public const string Latest = "$";

        public const string Earliest = "0";

        string _str = string.Empty;

        public long Head { get; set; }

        public long Tail { get; set; }

        /// <summary>
        /// RedisID
        /// </summary>
        /// <param name="head"></param>
        /// <param name="tail"></param>
        public RedisID(long head, long tail)
        {
            Head = head;
            Tail = tail;
            _str = $"{Head}-{Tail}";
        }

        /// <summary>
        /// RedisID
        /// </summary>
        /// <param name="redisId"></param>
        public RedisID(string redisId)
        {
            if (!string.IsNullOrEmpty(redisId) && redisId.IndexOf("-") > 0)
            {
                var arr = redisId.Split("-");
                if (arr.Length == 2)
                {
                    if (long.TryParse(arr[0], out long h) && long.TryParse(arr[1], out long t))
                    {
                        Head = h;
                        Tail = t;
                        _str = $"{Head}-{Tail}";
                    }
                }
            }
            else
            {
                _str = redisId.TrimEnd();
            }
        }

        /// <summary>
        /// RedisID
        /// </summary>
        public RedisID()
        {
            _str = Earliest;
        }

        /// <summary>
        /// 转换成字符串
        /// </summary>
        /// <returns></returns>
        public new string ToString()
        {
            return _str;
        }
    }


}
