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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： SynchronizingSubscriptionsFailedHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：e92a3a62-bc14-49ef-9c8c-6139e41b1cde
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public class SynchronizingSubscriptionsFailedHandlerDelegate : ISynchronizingSubscriptionsFailedHandler
    {
        private readonly Func<ManagedProcessFailedEventArgs, Task> _handler;

        public SynchronizingSubscriptionsFailedHandlerDelegate(Action<ManagedProcessFailedEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = context =>
            {
                handler(context);
                return Task.FromResult(0);
            };
        }

        public SynchronizingSubscriptionsFailedHandlerDelegate(Func<ManagedProcessFailedEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task HandleSynchronizingSubscriptionsFailedAsync(ManagedProcessFailedEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
