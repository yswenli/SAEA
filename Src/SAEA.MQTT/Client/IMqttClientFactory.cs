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
*命名空间：SAEA.MQTT.Client
*文件名： IMqttClientFactory
*版本号： v26.4.23.1
*唯一标识：4024d39e-e405-4c9d-96e3-ffdec882ff2d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClientFactory接口
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
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.LowLevelClient;

namespace SAEA.MQTT.Client
{
    /// <summary>
    /// MQTT 客户端与低层客户端的工厂接口
    /// </summary>
    public interface IMqttClientFactory
    {
        /// <summary>
        /// 设置默认的客户端适配器工厂，后续创建客户端时复用
        /// </summary>
        /// <param name="clientAdapterFactory">客户端适配器工厂，不能为 null</param>
        /// <returns>当前工厂实例，便于链式调用</returns>
        IMqttFactory UseClientAdapterFactory(IMqttClientAdapterFactory clientAdapterFactory);

        /// <summary>
        /// 用默认日志记录器与默认适配器工厂创建低层客户端
        /// </summary>
        /// <returns>低层 MQTT 客户端</returns>
        ILowLevelMqttClient CreateLowLevelMqttClient();

        /// <summary>
        /// 用指定日志记录器与默认适配器工厂创建低层客户端
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <returns>低层 MQTT 客户端</returns>
        ILowLevelMqttClient CreateLowLevelMqttClient(IMqttNetLogger logger);

        /// <summary>
        /// 用指定适配器工厂与默认日志记录器创建低层客户端
        /// </summary>
        /// <param name="clientAdapterFactory">客户端适配器工厂</param>
        /// <returns>低层 MQTT 客户端</returns>
        ILowLevelMqttClient CreateLowLevelMqttClient(IMqttClientAdapterFactory clientAdapterFactory);

        /// <summary>
        /// 用指定的日志记录器与适配器工厂创建低层客户端
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="clientAdapterFactory">客户端适配器工厂</param>
        /// <returns>低层 MQTT 客户端</returns>
        ILowLevelMqttClient CreateLowLevelMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory clientAdapterFactory);

        /// <summary>
        /// 用默认日志记录器与默认适配器工厂创建 MQTT 客户端
        /// </summary>
        /// <returns>MQTT 客户端</returns>
        IMqttClient CreateMqttClient();

        /// <summary>
        /// 用指定日志记录器与默认适配器工厂创建 MQTT 客户端
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <returns>MQTT 客户端</returns>
        IMqttClient CreateMqttClient(IMqttNetLogger logger);

        /// <summary>
        /// 用指定适配器工厂与默认日志记录器创建 MQTT 客户端
        /// </summary>
        /// <param name="adapterFactory">客户端适配器工厂</param>
        /// <returns>MQTT 客户端</returns>
        IMqttClient CreateMqttClient(IMqttClientAdapterFactory adapterFactory);

        /// <summary>
        /// 用指定的日志记录器与适配器工厂创建 MQTT 客户端
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="adapterFactory">客户端适配器工厂</param>
        /// <returns>MQTT 客户端</returns>
        IMqttClient CreateMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory adapterFactory);
    }
}