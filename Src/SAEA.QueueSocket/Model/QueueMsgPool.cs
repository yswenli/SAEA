/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueMsgPool
*版本号： v7.0.0.1
*唯一标识：a1b2c3d4-e5f6-7890-abcd-ef1234567890
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2025/4/19
*描述：
*    QueueMsg 对象池，用于复用 QueueMsg 实例，减少内存分配
*
*=====================================================================
*修改标记
*修改时间：2025/4/19
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*    初始创建，实现 QueueMsg 对象池
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;

using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// QueueMsg 对象池，用于复用 QueueMsg 实例
    /// </summary>
    public static class QueueMsgPool
    {
        private static readonly ConcurrentBag<QueueMsg> _pool = new ConcurrentBag<QueueMsg>();

        /// <summary>
        /// 从池中获取 QueueMsg 实例
        /// </summary>
        public static QueueMsg Rent()
        {
            if (_pool.TryTake(out var msg))
            {
                // 重置状态
                msg.Type = QueueSocketMsgType.Ping;
                msg.Name = null;
                msg.Topic = null;
                msg.Data = null;
                msg.IsPooled = false;
                return msg;
            }
            return new QueueMsg();
        }

        /// <summary>
        /// 将 QueueMsg 归还到池
        /// </summary>
        public static void Return(QueueMsg msg)
        {
            if (msg != null)
            {
                msg.Dispose();
                _pool.Add(msg);
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
            while (_pool.TryTake(out _)) { }
        }
    }
}
