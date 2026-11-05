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
 *文件名： MqttMessageExpiry
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c06
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttMessageExpiry类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttMessageExpiry类
 *
 *****************************************************************************/
using System;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// MQTT 5.0 Message Expiry Interval 计算帮助类。
    /// 绝对到期时间统一由调用方基于 <see cref="MqttTime.UtcNow"/> 计算，便于假时钟测试。
    /// </summary>
    internal static class MqttMessageExpiry
    {
        /// <summary>
        /// 根据 Message Expiry Interval（秒）计算绝对到期时间。
        /// interval 为 null 时返回 null，表示永不过期。
        /// </summary>
        public static DateTimeOffset? GetExpiresAt(uint? messageExpiryInterval, DateTimeOffset now)
        {
            if (!messageExpiryInterval.HasValue)
            {
                return null;
            }

            return now.AddSeconds(messageExpiryInterval.Value);
        }

        /// <summary>
        /// 计算剩余整秒（向上取整）。已过期返回 0；无到期时间返回 null。
        /// 结果上限为 <see cref="uint.MaxValue"/>。
        /// </summary>
        public static uint? GetRemainingSeconds(DateTimeOffset? expiresAt, DateTimeOffset now)
        {
            if (!expiresAt.HasValue)
            {
                return null;
            }

            var remainingSeconds = (expiresAt.Value - now).TotalSeconds;
            if (remainingSeconds <= 0)
            {
                return 0;
            }

            var roundedSeconds = Math.Ceiling(remainingSeconds);
            if (roundedSeconds >= uint.MaxValue)
            {
                return uint.MaxValue;
            }

            return (uint)roundedSeconds;
        }
    }
}