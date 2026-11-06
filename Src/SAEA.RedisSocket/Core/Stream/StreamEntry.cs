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
*文件名： StreamEntry
*版本号： v26.4.23.1
*唯一标识：b1e9ea80-4ad6-40fc-a05c-540e7f2a2fbf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/09 13:47:20
*描述：StreamEntry类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// Stream 消息条目，包含所属主题与各消息 ID 的字段内容
    /// </summary>
    public class StreamEntry
    {
        string _topic;
        /// <summary>
        /// 所属主题，即 Stream 的键名，赋值时去除尾部空白
        /// </summary>
        public string Topic {
            get
            {
                return _topic;
            }
            set
            {
                _topic = value?.TrimEnd();
            }
        }

        /// <summary>
        /// 消息 ID 及其字段集合
        /// </summary>
        public IEnumerable<IdFiled> IdFileds { get; set; }
    }

    /// <summary>
    /// 单个消息 ID 及其字段集合
    /// </summary>
    public class IdFiled
    {
        /// <summary>
        /// 消息 ID
        /// </summary>
        public RedisID RedisID { get; set; }

        /// <summary>
        /// 该消息包含的字段键值对
        /// </summary>
        public IEnumerable<RedisField> RedisFields { get; set; }
    }
}
