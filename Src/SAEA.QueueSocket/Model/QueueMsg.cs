/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueResult
*版本号： v26.4.23.1
*唯一标识：bfbbfbe7-acd9-4f83-9f6a-e03890205a5b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/8 16:08:47
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/8 16:08:47
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// 队列编辑消息实体
    /// </summary>
    public class QueueMsg : IDisposable
    {
        public QueueSocketMsgType Type { get; set; }

        public string Name { get; set; }

        public string Topic { get; set; }

        public byte[] Data { get; set; }

        /// <summary>
        /// 标记 Data 是否来自 ArrayPool
        /// </summary>
        internal bool IsPooled { get; set; }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (Data != null)
            {
                if (IsPooled)
                {
                    // 归还到 ArrayPool
                    System.Buffers.ArrayPool<byte>.Shared.Return(Data, clearArray: false);
                }
                else if (Data.Length > 0)
                {
                    Array.Clear(Data, 0, Data.Length);
                }
                Data = null;
            }
            IsPooled = false;
        }
    }
}