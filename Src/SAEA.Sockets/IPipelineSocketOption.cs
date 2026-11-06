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
 *文件名： IPipelineSocketOption
 *描述：管道钩子选项
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