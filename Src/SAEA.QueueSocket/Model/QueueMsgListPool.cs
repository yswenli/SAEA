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
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueMsgListPool
*版本号： v26.4.23.1
*唯一标识：36af62fb-01eb-4937-823a-2614d73788c0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/19 02:48:05
*描述：QueueMsgListPool接口
*
*=====================================================================
*修改标记
*修改时间：2026/04/19 02:48:05
*修改人： yswenli
*版本号： v26.4.23.1
*描述：QueueMsgListPool接口
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