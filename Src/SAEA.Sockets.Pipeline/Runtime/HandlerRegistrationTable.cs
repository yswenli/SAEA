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
*文件名： HandlerRegistrationTable
*版本号： v26.11.5.1
*唯一标识：2085609f-10cc-489f-9d68-9276b948a39b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：处理器活跃注册计数表：判定 IChannelHandler.IsSharable 是否允许同一实例挂到多条链
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：处理器活跃注册计数表：判定 IChannelHandler.IsSharable 是否允许同一实例挂到多条链
*
*****************************************************************************/
using System;
using System.Runtime.CompilerServices;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 处理器活跃注册计数表：判定 <see cref="IChannelHandler.IsSharable"/> 是否允许同一实例挂到多条链。
    /// 条目以弱引用持有处理器，计数归零时即解除，避免静态表把处理器钉死。
    /// </summary>
    internal static class HandlerRegistrationTable
    {
        private sealed class Registration
        {
            public int Count;
        }

        private static readonly ConditionalWeakTable<IChannelHandler, Registration> Table =
            new ConditionalWeakTable<IChannelHandler, Registration>();

        private static readonly object Gate = new object();

        /// <summary>
        /// 登记一次活跃引用。非共享处理器已有其它活跃登记时抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        public static void Acquire(IChannelHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (Gate)
            {
                var registration = Table.GetOrCreateValue(handler);

                if (registration.Count > 0 && !handler.IsSharable)
                {
                    throw new InvalidOperationException(
                        "处理器 " + handler.GetType().FullName +
                        " 的 IsSharable 为 false，同一实例不能同时注册到多条通道链；先从原链移除再挂载。");
                }

                registration.Count++;
            }
        }

        /// <summary>
        /// 归还一次活跃引用；计数归零后解除条目。重复调用不会让计数变成负数。
        /// </summary>
        public static void Release(IChannelHandler handler)
        {
            if (handler == null)
                return;

            lock (Gate)
            {
                Registration registration;
                if (!Table.TryGetValue(handler, out registration))
                    return;

                if (registration.Count > 0)
                    registration.Count--;

                if (registration.Count == 0)
                    Table.Remove(handler);
            }
        }
    }
}