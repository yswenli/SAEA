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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// RedisID
    /// </summary>
    public struct RedisID
    {
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
        }

        /// <summary>
        /// 转换成字符串
        /// </summary>
        /// <returns></returns>
        public new string ToString()
        {
            return $"{Head}-{Tail}";
        }

        /// <summary>
        /// 将字符串转换成RedisID,不成功则返回最小值
        /// </summary>
        /// <param name="redisId"></param>
        /// <returns></returns>
        public static RedisID Parse(string redisId)
        {
            if (!string.IsNullOrEmpty(redisId) && redisId.IndexOf("-") > 0)
            {
                var arr = redisId.Split("-");
                if (arr.Length == 2)
                {
                    if (long.TryParse(arr[0], out long h) && long.TryParse(arr[1], out long t))
                    {
                        return new RedisID(h, t);
                    }
                }

            }
            return new RedisID(0, 1); ;
        }
    }


}
