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
*命名空间：SAEA.MQTT.Implementations
*文件名： PlatformAbstractionLayer
*版本号： v26.4.23.1
*唯一标识：10726f8e-0867-4862-bd0d-e45e2d02dc65
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
using System.Threading.Tasks;

namespace SAEA.MQTT.Implementations
{
    public static class PlatformAbstractionLayer
    {
#if NET452
        public static Task CompletedTask => Task.FromResult(0);

        public static byte[] EmptyByteArray { get; } = new byte[0];
#else
        public static Task CompletedTask => Task.CompletedTask;

        public static byte[] EmptyByteArray { get; } = Array.Empty<byte>();
#endif

        public static void Sleep(TimeSpan timeout)
        {
#if NET452 || NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP3_1
            System.Threading.Thread.Sleep(timeout);
#else
            Task.Delay(timeout).Wait();
#endif
        }
    }
}
