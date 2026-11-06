/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets
*文件名： IReceiveFlowControl
*版本号： v26.11.5.1
*唯一标识：9fcc819b-894a-48f6-b451-a89472e2666f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：接收流量控制钩子
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
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