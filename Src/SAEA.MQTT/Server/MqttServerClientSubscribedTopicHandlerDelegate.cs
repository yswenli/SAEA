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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerClientSubscribedTopicHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：755bcf19-8379-4133-8c47-99065c003bef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientSubscribedTopicHandlerDelegate接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttServerClientSubscribedTopicHandlerDelegate接口
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public class MqttServerClientSubscribedHandlerDelegate : IMqttServerClientSubscribedTopicHandler
    {
        private readonly Func<MqttServerClientSubscribedTopicEventArgs, Task> _handler;

        public MqttServerClientSubscribedHandlerDelegate(Action<MqttServerClientSubscribedTopicEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = eventArgs =>
            {
                handler(eventArgs);
                return Task.FromResult(0);
            };
        }

        public MqttServerClientSubscribedHandlerDelegate(Func<MqttServerClientSubscribedTopicEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task HandleClientSubscribedTopicAsync(MqttServerClientSubscribedTopicEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
