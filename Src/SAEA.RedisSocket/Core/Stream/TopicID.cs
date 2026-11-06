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
*文件名： TopicID
*版本号： v26.4.23.1
*唯一标识：e57d0fbb-e402-4242-be0d-a092a9eb5dda
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：TopicID接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// Stream 主题与起始消息 ID 的组合，用于指定读取位置
    /// </summary>
    public struct TopicID
    {
        /// <summary>
        /// 主题名，即 Stream 的键名
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// 起始消息 ID，可用 $ 或 0 等特殊标记
        /// </summary>
        public string RedisID { get; set; }

        /// <summary>
        /// 以主题名与起始消息 ID 构造
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="redisID">起始消息 ID</param>
        public TopicID(string topic,string redisID)
        {
            Topic = topic;
            RedisID = redisID;
        }
    }
}
