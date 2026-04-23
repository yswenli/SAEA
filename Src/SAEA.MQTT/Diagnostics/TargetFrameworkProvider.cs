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
*文件名： TargetFrameworkProvider
*版本号： v26.4.23.1
*唯一标识：77474942-8cf4-434e-bf4a-91eed037909e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：TargetFrameworkProvider接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TargetFrameworkProvider接口
*
*****************************************************************************/
namespace SAEA.MQTT.Diagnostics
{
    public static class TargetFrameworkProvider
    {
        public static string TargetFramework
        {
            get
            {
#if NET452
                return "net452";
#elif NET461
                return "net461";
#elif NET472
                return "net472";
#elif NETSTANDARD1_3
                return "netstandard1.3";
#elif NETSTANDARD2_0
                return "netstandard2.0";
#elif NETSTANDARD2_1
                return "netstandard2.1";
#elif WINDOWS_UWP
                return "uap10.0";
#elif NETCOREAPP3_1
                return "netcoreapp3.1";
#endif
            }
        }
    }
}