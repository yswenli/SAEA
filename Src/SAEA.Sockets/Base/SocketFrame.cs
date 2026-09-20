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
 *命名空间：SAEA.Sockets.Base
 *文件名： SocketFrame
 *版本号： v26.4.23.1
 *唯一标识：b7c4d1e2-9f3a-4c6d-8e21-0a5b6c7d8e9f
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/09/20 17:07:21
 *描述：SocketFrame零拷贝单帧视图
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/09/20 17:07:21
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：SocketFrame零拷贝单帧视图
 *
 *****************************************************************************/
using System;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 单帧视图。<see cref="Content"/> 仅在回调期间有效，回调返回后不得引用。
    /// </summary>
    public readonly ref struct SocketFrame
    {
        /// <summary>
        /// 帧体长度
        /// </summary>
        public readonly long BodyLength;

        /// <summary>
        /// 帧类型
        /// </summary>
        public readonly byte Type;

        /// <summary>
        /// 帧体切片（仅在回调期间有效）
        /// </summary>
        public readonly ReadOnlySpan<byte> Content;

        /// <summary>
        /// 构造单帧视图
        /// </summary>
        /// <param name="bodyLength">帧体长度</param>
        /// <param name="type">帧类型</param>
        /// <param name="content">帧体切片</param>
        public SocketFrame(long bodyLength, byte type, ReadOnlySpan<byte> content)
        {
            BodyLength = bodyLength;
            Type = type;
            Content = content;
        }
    }
}