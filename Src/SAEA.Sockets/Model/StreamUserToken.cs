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
 *命名空间：SAEA.Sockets.Model
 *文件名： StreamUserToken
 *版本号： v26.4.23.1
 *唯一标识：7f3c2b8a-1d64-4e9b-a5c2-0e6f9b1d4a37
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2019/02/11 17:03:06
 *描述：StreamUserToken
 *
 *=====================================================================
 *修改标记
 *修改时间：2019/02/11 17:03:06
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：StreamUserToken
 *
 *****************************************************************************/
using System.IO;
using System.IO.Pipelines;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// Stream 通道的用户令牌，承载连接流与其 PipeReader 输入。
    /// </summary>
    /// <remarks>
    /// 本令牌用于 Stream 后端。继承自 <see cref="SAEA.Sockets.Base.BaseUserToken"/> 的 IOCP 专用成员
    /// （<c>ReadArgs</c>/<c>WriteArgs</c>/<c>IsSending</c>/<c>WaitWrite</c>/<c>ReleaseWrite</c>）在 Stream 场景
    /// 不适用，消费方只应依赖 <see cref="SAEA.Sockets.Interface.IUserToken.ID"/>、<see cref="Stream"/>、
    /// <see cref="Input"/>（以及 <see cref="SAEA.Sockets.Interface.IUserToken.Coder"/>）。
    /// <see cref="Stream"/> 与该连接的 <see cref="ChannelInfo.Stream"/> 是同一实例，不得独立重新赋值。
    /// </remarks>
    public class StreamUserToken : SAEA.Sockets.Base.BaseUserToken
    {
        /// <summary>
        /// 获取或设置通道的网络流
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// 获取或设置通道的 PipeReader
        /// </summary>
        public PipeReader Input { get; set; }
    }
}
