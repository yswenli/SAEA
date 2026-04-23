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
*文件名： TaskExtensions
*版本号： v26.4.23.1
*唯一标识：540a5277-8e08-43a4-8f6e-fb27d48f7b05
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：TaskExtensions接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TaskExtensions接口
*
*****************************************************************************/
using SAEA.MQTT.Diagnostics;
using System.Threading.Tasks;

namespace SAEA.MQTT.Internal
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task, IMqttNetScopedLogger logger)
        {
            task?.ContinueWith(t =>
                {
                    logger.Error(t.Exception, "Unhandled exception.");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
