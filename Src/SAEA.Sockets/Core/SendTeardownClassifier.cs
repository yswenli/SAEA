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
*命名空间：SAEA.Sockets.Core
*文件名： SendTeardownClassifier
*版本号： v26.11.27.1
*唯一标识：81df8265-a922-42f5-acfe-53ad2540f7a2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/28 14:01:10
*描述：发送失败收尾分级判定
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 14:01:10
*修改人： yswenli
*版本号： v26.11.27.1
*描述：新增，从 SaeaServerSocket 抽取发送失败收尾分级判定
*
 *****************************************************************************/
using System;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 发送失败收尾分级：识别连接关闭、重置或回收等预期失败。
    /// </summary>
    internal static class SendTeardownClassifier
    {
        /// <summary>
        /// 判断发送失败是否属于连接收尾阶段的预期结果。
        /// <para>
        /// <see cref="SocketError.NetworkDown"/>、<see cref="SocketError.NetworkUnreachable"/>、
        /// <see cref="SocketError.HostUnreachable"/> 未列入降级集合：它们是真实网络故障，不能吞成收尾。
        /// </para>
        /// </summary>
        /// <param name="exception">发送路径上抛出的异常</param>
        /// <returns>属于连接关闭、重置或回收等预期收尾失败时返回 true</returns>
        internal static bool IsExpectedSendTeardown(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is ObjectDisposedException) return true;

                var socketException = current as SocketException;
                if (socketException == null) continue;

                switch (socketException.SocketErrorCode)
                {
                    case SocketError.NotConnected:
                    case SocketError.ConnectionReset:
                    case SocketError.ConnectionAborted:
                    case SocketError.NetworkReset:
                    case SocketError.Shutdown:
                    case SocketError.Disconnecting:
                    case SocketError.OperationAborted:
                        return true;
                }
            }

            return false;
        }
    }
}
