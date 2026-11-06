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
*命名空间：SAEA.MQTT
*文件名： IMqttFactory
*版本号： v26.4.23.1
*唯一标识：d401dc05-18e0-4b91-a36b-4763913431c7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttFactory工厂类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Client;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Server;

namespace SAEA.MQTT
{
    /// <summary>
    /// MQTT 工厂接口，统一提供客户端、服务端及底层客户端的创建入口
    /// </summary>
    public interface IMqttFactory : IMqttClientFactory, IMqttServerFactory
    {
        /// <summary>
        /// 创建客户端与服务端时使用的默认日志记录器
        /// </summary>
        IMqttNetLogger DefaultLogger { get; }

        /// <summary>
        /// 供扩展逻辑存放自定义数据的属性字典，实现内部不读取
        /// </summary>
        IDictionary<object, object> Properties { get; }
    }
}
