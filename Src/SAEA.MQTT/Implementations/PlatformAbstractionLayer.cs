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
*唯一标识：e428ddc4-2d7d-418e-9ad9-d31a015d9be6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：PlatformAbstractionLayer接口
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
using System.Threading.Tasks;

namespace SAEA.MQTT.Implementations
{
    /// <summary>
    /// 跨目标框架的兼容层，屏蔽不同框架在已完成任务与空数组上的差异
    /// </summary>
    public static class PlatformAbstractionLayer
    {
#if NET452
        /// <summary>
        /// 已完成的空任务，用于需要立即返回 Task 的同步实现
        /// </summary>
        public static Task CompletedTask => Task.FromResult(0);

        /// <summary>
        /// 共享的空字节数组，调用方不得写入
        /// </summary>
        public static byte[] EmptyByteArray { get; } = new byte[0];
#else
        /// <summary>
        /// 已完成的空任务，用于需要立即返回 Task 的同步实现
        /// </summary>
        public static Task CompletedTask => Task.CompletedTask;

        /// <summary>
        /// 共享的空字节数组，调用方不得写入
        /// </summary>
        public static byte[] EmptyByteArray { get; } = Array.Empty<byte>();
#endif

        /// <summary>
        /// 同步阻塞当前线程指定时长
        /// </summary>
        /// <param name="timeout">阻塞时长，负值会由底层 Sleep 抛出异常</param>
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
