/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： ChannelClosedException
*版本号： v26.11.5.1
*唯一标识：7e038cdc-95da-4d83-bd78-88ab2c54051c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：连接已关闭或已进入收口，仍尝试写数据时抛出
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：连接已关闭或已进入收口，仍尝试写数据时抛出
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接已关闭或已进入收口，仍尝试写数据时抛出。
    /// </summary>
    public sealed class ChannelClosedException : Exception
    {
        /// <summary>创建带说明的已关闭异常。</summary>
        public ChannelClosedException(string message)
            : base(message)
        {
        }

        /// <summary>创建带说明与内部异常的已关闭异常。</summary>
        public ChannelClosedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}