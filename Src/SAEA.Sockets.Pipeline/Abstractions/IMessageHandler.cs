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
*命名空间：SAEA.Sockets.Pipeline
*文件名： IMessageHandler
*版本号： v26.11.5.1
*唯一标识：62bd8091-33f4-4ff9-811e-6754af0a6be7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：业务消息处理器：由消息分发器按消息的精确 CLR 类型装配并调用
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：业务消息处理器：由消息分发器按消息的精确 CLR 类型装配并调用
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 业务消息处理器：由消息分发器按消息的精确 CLR 类型装配并调用。
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// 处理一条业务消息。<paramref name="ctx"/> 是该消息所在链节点的上下文，
        /// 回包必须使用它，不要另建指向链首的上下文。
        /// </summary>
        Task HandleAsync(IChannelHandlerContext ctx, object message);
    }
}