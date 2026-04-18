/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueMsgListPool
*版本号： v7.0.0.1
*唯一标识：b2c3d4e5-f6a7-8901-bcde-f23456789012
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2025/4/19
*描述：
*    QueueMsg List 对象池，用于复用 List<QueueMsg> 实例
*
*=====================================================================
*修改标记
*修改时间：2025/4/19
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*    初始创建，实现 List<QueueMsg> 对象池
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// QueueMsg List 对象池
    /// </summary>
    public static class QueueMsgListPool
    {
        private static readonly ConcurrentBag<List<QueueMsg>> _pool = new ConcurrentBag<List<QueueMsg>>();

        /// <summary>
        /// 从池中获取 List<QueueMsg> 实例
        /// </summary>
        public static List<QueueMsg> Rent()
        {
            if (_pool.TryTake(out var list))
            {
                list.Clear();
                return list;
            }
            return new List<QueueMsg>(16); // 预分配初始容量
        }

        /// <summary>
        /// 将 List<QueueMsg> 归还到池
        /// </summary>
        public static void Return(List<QueueMsg> list)
        {
            if (list != null)
            {
                // 归还其中的 QueueMsg 到对象池
                foreach (var msg in list)
                {
                    QueueMsgPool.Return(msg);
                }
                list.Clear();
                _pool.Add(list);
            }
        }

        /// <summary>
        /// 获取池中对象数量（用于诊断）
        /// </summary>
        public static int Count => _pool.Count;

        /// <summary>
        /// 清空池
        /// </summary>
        public static void Clear()
        {
            while (_pool.TryTake(out var list))
            {
                foreach (var msg in list)
                {
                    QueueMsgPool.Return(msg);
                }
            }
        }
    }
}
