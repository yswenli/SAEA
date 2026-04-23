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
*文件名： MqttServerConnectionValidatorDelegate
*版本号： v26.4.23.1
*唯一标识：0e89801f-987b-41ca-a890-fd712ce64d9d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerConnectionValidatorDelegate接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttServerConnectionValidatorDelegate接口
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public class MqttServerConnectionValidatorDelegate : IMqttServerConnectionValidator
    {
        private readonly Func<MqttConnectionValidatorContext, Task> _callback;

        public MqttServerConnectionValidatorDelegate(Action<MqttConnectionValidatorContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            _callback = context =>
            {
                callback(context);
                return Task.FromResult(0);
            };
        }

        public MqttServerConnectionValidatorDelegate(Func<MqttConnectionValidatorContext, Task> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public Task ValidateConnectionAsync(MqttConnectionValidatorContext context)
        {
            return _callback(context);
        }
    }
}
