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
*唯一标识：7e01e9d2-beb3-43e1-9f47-d342cda71095
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT诊断日志类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT诊断日志类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Diagnostics
{
    public sealed class MqttNetLogMessage
    {
        public string LogId { get; set; }

        public DateTime Timestamp { get; set; }

        public int ThreadId { get; set; }

        public string Source { get; set; }

        public MqttNetLogLevel Level { get; set; }

        public string Message { get; set; }

        public Exception Exception { get; set; }

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
