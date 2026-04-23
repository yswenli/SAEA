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
*命名空间：SAEA.RedisSocket.Core.Stream
*文件名： RedisID
*版本号： v26.4.23.1
*唯一标识：7b41a24e-b901-4059-94f5-a8a1a521e602
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：RedisID接口
*
*=====================================================================
*修改标记
*修改时间：2021/01/08 20:00:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisID接口
*
*****************************************************************************/
using SAEA.Common;

using System;

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
                var arr = redisId.Split(new string[] { "-" }, StringSplitOptions.None);
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
