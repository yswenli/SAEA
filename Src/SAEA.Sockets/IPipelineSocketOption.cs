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
*文件名： IPipelineSocketOption
*版本号： v26.11.5.1
*唯一标识：889e7c8c-9d4d-4384-b8a9-20cb66b5ff17
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：管道钩子选项
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets
{
    /// <summary>
    /// 承载连接建立钩子与输入处理器钩子的套接字选项。
    /// </summary>
    public interface IPipelineSocketOption
    {
        /// <summary>
        /// 连接建立回调，在通道登记完成后、通知 OnAccepted 之前同步调用；未配置时为 null。
        /// </summary>
        Action<ChannelInfo> ConnectionOpened { get; }

        /// <summary>
        /// 输入处理器，接管该连接的读循环；未配置时为 null。
        /// </summary>
        Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> InputHandler { get; }
    }
}