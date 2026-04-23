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
*命名空间：SAEA.MQTT.Extensions
*文件名： UserPropertyExtension
*版本号： v26.4.23.1
*唯一标识：f919635f-8d1e-43fd-8212-2ffc50bbf02a
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
using System;
using System.Linq;

namespace SAEA.MQTT.Extensions
{
    public static class UserPropertyExtension
    {
        public static string GetUserProperty(this MqttApplicationMessage message, string propertyName, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

            return message.UserProperties?.SingleOrDefault(up => up.Name.Equals(propertyName, comparisonType))?.Value;
        }
    }
}
