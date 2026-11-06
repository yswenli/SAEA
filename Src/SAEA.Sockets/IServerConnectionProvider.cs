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
 *文件名： IServerConnectionProvider
 *描述：TCP 服务端每连接流 / TLS 信息访问
 *
 *****************************************************************************/
using System.IO;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Ssl;

namespace SAEA.Sockets
{
    /// <summary>
    /// TCP 服务端每连接流 / TLS 信息访问。
    /// </summary>
    public interface IServerConnectionProvider
    {
        /// <summary>
        /// 获取指定用户令牌对应的双工流。
        /// </summary>
        /// <param name="userToken">目标连接的用户令牌</param>
        /// <returns>连接的双工流；令牌为 null、ID 为空或会话不存在时返回 null</returns>
        Stream GetStream(IUserToken userToken);

        /// <summary>
        /// 获取指定用户令牌对应的 TLS 状态；未启用 TLS 时返回 null。
        /// </summary>
        /// <param name="userToken">目标连接的用户令牌</param>
        /// <returns>该连接的 TLS 上下文；令牌无效、会话不存在或未启用 TLS 时返回 null</returns>
        ISaeaTlsContext GetTlsContext(IUserToken userToken);
    }
}