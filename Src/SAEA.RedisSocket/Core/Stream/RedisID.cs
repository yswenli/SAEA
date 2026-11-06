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
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;

using System;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// Stream 消息 ID，由时间戳与序列号组成，格式为 head-tail
    /// </summary>
    public class RedisID
    {
        /// <summary>
        /// 仅读取新消息的位置标记 $
        /// </summary>
        public const string Latest = "$";

        /// <summary>
        /// 从头读取的位置标记 0
        /// </summary>
        public const string Earliest = "0";

        string _str = string.Empty;

        /// <summary>
        /// ID 的时间戳部分，单位毫秒
        /// </summary>
        public long Head { get; set; }

        /// <summary>
        /// ID 的序列号部分
        /// </summary>
        public long Tail { get; set; }

        /// <summary>
        /// 以时间戳与序列号构造 ID
        /// </summary>
        /// <param name="head">时间戳部分，单位毫秒</param>
        /// <param name="tail">序列号部分</param>
        public RedisID(long head, long tail)
        {
            Head = head;
            Tail = tail;
            _str = $"{Head}-{Tail}";
        }

        /// <summary>
        /// 解析 head-tail 格式的字符串构造 ID，解析失败时保留原始文本且时间戳与序列号均为 0
        /// </summary>
        /// <param name="redisId">ID 字符串，也可是 $ 或 0 等特殊标记</param>
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
        /// 创建取值为从头读取标记的 ID
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
