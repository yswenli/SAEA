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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttTime
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c03
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttTime类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttTime类
 *
 *****************************************************************************/
using System;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 全局时间门面。内部静态可变状态仅供测试替换，生产路径只读取 <see cref="UtcNow"/>。
    /// </summary>
    internal static class MqttTime
    {
        static IMqttTimeProvider _provider = MqttSystemTimeProvider.Instance;

        public static IMqttTimeProvider Provider
        {
            get { return _provider; }
            set { _provider = value ?? MqttSystemTimeProvider.Instance; }
        }

        public static DateTimeOffset UtcNow
        {
            get { return _provider.UtcNow; }
        }
    }
}