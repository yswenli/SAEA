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
*文件名： MqttFactoryExtensions
*版本号： v26.4.23.1
*唯一标识：ee64a28a-c1f9-40a2-8b12-6ae040e3157e
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
using SAEA.MQTT.Diagnostics;
using System;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public static class MqttFactoryExtensions
    {
        public static IManagedMqttClient CreateManagedMqttClient(this IMqttFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            return new ManagedMqttClient(factory.CreateMqttClient(), factory.DefaultLogger);
        }

        public static IManagedMqttClient CreateManagedMqttClient(this IMqttFactory factory, IMqttNetLogger logger)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new ManagedMqttClient(factory.CreateMqttClient(logger), logger);
        }
    }
}