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
*文件名： QueueMsg
*版本号： v26.4.23.1
*唯一标识：f956e8af-2972-4566-ab1f-c21bbf4085e7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：QueueMsg类
*
*=====================================================================
*修改标记
*修改时间：2025/02/10 17:07:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：QueueMsg类
*
*****************************************************************************/
using System;

using SAEA.Common.Caching;
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

        public ReadOnlyMemory<byte> Data { get; set; }

        PooledBuffer _owner;

        public void SetOwner(PooledBuffer owner)
        {
            _owner = owner;
            if (owner != null) Data = owner.AsMemory();
        }

        public PooledBuffer DetachOwner()
        {
            var owner = _owner;
            _owner = null;
            return owner;
        }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            Data = ReadOnlyMemory<byte>.Empty;
            if (owner != null) owner.Dispose();
        }

        internal void Reset()
        {
            _owner = null;
            Data = ReadOnlyMemory<byte>.Empty;
        }
    }
}