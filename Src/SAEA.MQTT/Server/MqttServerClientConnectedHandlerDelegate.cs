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
*文件名： MqttServerClientConnectedHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：8eab9617-9971-4a70-ae13-f7f44bdf1187
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientConnectedHandlerDelegate接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttServerClientConnectedHandlerDelegate接口
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public class MqttServerClientConnectedHandlerDelegate : IMqttServerClientConnectedHandler
    {
        private readonly Func<MqttServerClientConnectedEventArgs, Task> _handler;

        public MqttServerClientConnectedHandlerDelegate(Action<MqttServerClientConnectedEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = eventArgs =>
            {
                handler(eventArgs);
                return Task.FromResult(0);
            };
        }

        public MqttServerClientConnectedHandlerDelegate(Func<MqttServerClientConnectedEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task HandleClientConnectedAsync(MqttServerClientConnectedEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
