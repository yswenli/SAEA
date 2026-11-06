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
*命名空间：SAEA.MQTT.Client.Options
*文件名： MqttClientTcpOptionsExtensions
*版本号： v26.4.23.1
*唯一标识：6b109e65-da90-498f-a032-938c269feea4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientTcpOptionsExtensions接口
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

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// TCP 通道选项的扩展方法
    /// </summary>
    public static class MqttClientTcpOptionsExtensions
    {
        /// <summary>
        /// 取实际使用的端口，未显式设置时按 TLS 是否启用返回 1883 或 8883
        /// </summary>
        /// <param name="options">TCP 通道选项，不能为 null</param>
        /// <returns>实际使用的端口号</returns>
        public static int GetPort(this MqttClientTcpOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.Port.HasValue)
            {
                return options.Port.Value;
            }

            return !options.TlsOptions.UseTls ? 1883 : 8883;
        }
    }
}
