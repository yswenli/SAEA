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
*命名空间：SAEA.MQTT.Diagnostics
*文件名： MqttNetLogMessage
*版本号： v26.4.23.1
*唯一标识：dfa0b213-3870-4519-9aa0-af377fe7c04f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttNetLogMessage消息类
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

namespace SAEA.MQTT.Diagnostics
{
    /// <summary>
    /// 一条日志消息的数据载体，由日志记录器组装后分发
    /// </summary>
    public sealed class MqttNetLogMessage
    {
        /// <summary>
        /// 产生该日志的记录器标识，未指定时为 null
        /// </summary>
        public string LogId { get; set; }

        /// <summary>
        /// 日志产生时间，UTC
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 产生日志的托管线程标识
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// 日志来源标识，通常为组件名
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public MqttNetLogLevel Level { get; set; }

        /// <summary>
        /// 已按参数格式化后的消息文本
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 关联异常，无异常时为 null
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 输出单行文本形式的日志，存在异常时在其后追加换行与异常详情
        /// </summary>
        /// <returns>日志的文本表示</returns>
        public override string ToString()
        {
            var result = $"[{Timestamp:O}] [{LogId}] [{ThreadId}] [{Source}] [{Level}]: {Message}";
            if (Exception != null)
            {
                result += Environment.NewLine + Exception;
            }

            return result;
        }
    }
}
