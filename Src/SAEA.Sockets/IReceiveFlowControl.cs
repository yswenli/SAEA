/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets
 *文件名： IReceiveFlowControl
 *描述：接收流量控制钩子
 *
 *****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Sockets
{
    /// <summary>
    /// 接收流量控制。由接管读循环的输入处理器调用，作用于该连接的读取闸门。
    /// </summary>
    public interface IReceiveFlowControl
    {
        /// <summary>
        /// 是否已暂停读取。
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 暂停读取。幂等。
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复读取。幂等。
        /// </summary>
        void Resume();

        /// <summary>
        /// 关闭该连接。必须幂等：连接已关闭时直接返回。
        /// </summary>
        /// <param name="reason">关闭原因</param>
        /// <returns>关闭完成的任务；无需异步等待时返回已完成任务</returns>
        Task CloseAsync(string reason);
    }
}