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
*文件名： IMqttServerFactory
*版本号： v26.4.23.1
*唯一标识：9f187a1c-f903-408b-b37e-cc0c89c3097e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerFactory工厂类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Diagnostics;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// MQTT 服务端工厂，负责创建服务端实例并指定底层传输适配器
    /// </summary>
    public interface IMqttServerFactory
    {
        /// <summary>
        /// 默认的传输适配器创建函数集合，未显式指定适配器时逐个创建并启动
        /// </summary>
        IList<Func<IMqttFactory, IMqttServerAdapter>> DefaultServerAdapters { get; }

        /// <summary>
        /// 使用默认适配器与默认日志器创建服务端
        /// </summary>
        /// <returns>尚未启动的服务端实例</returns>
        IMqttServer CreateMqttServer();

        /// <summary>
        /// 使用默认适配器与指定日志器创建服务端
        /// </summary>
        /// <param name="logger">日志器，贯穿服务端各组件</param>
        /// <returns>尚未启动的服务端实例</returns>
        IMqttServer CreateMqttServer(IMqttNetLogger logger);

        /// <summary>
        /// 使用指定适配器与默认日志器创建服务端
        /// </summary>
        /// <param name="adapters">传输适配器集合，替代默认适配器</param>
        /// <returns>尚未启动的服务端实例</returns>
        IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> adapters);

        /// <summary>
        /// 使用指定适配器与指定日志器创建服务端
        /// </summary>
        /// <param name="adapters">传输适配器集合，替代默认适配器</param>
        /// <param name="logger">日志器，贯穿服务端各组件</param>
        /// <returns>尚未启动的服务端实例</returns>
        IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> adapters, IMqttNetLogger logger);
    }
}