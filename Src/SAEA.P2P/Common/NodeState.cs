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
*命名空间：SAEA.P2P.Common
*文件名： NodeState
*版本号： v26.4.23.1
*唯一标识：8524e9da-a611-4750-9f9a-dfcd4504a2c1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：NodeState状态枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.P2P.Common
{
    /// <summary>
    /// P2P 节点的运行状态
    /// </summary>
    public enum NodeState
    {
        /// <summary>
        /// 初始状态，尚未连接信令服务器
        /// </summary>
        Init = 0,
        /// <summary>
        /// 正在连接信令服务器
        /// </summary>
        Connecting = 1,
        /// <summary>
        /// 已发出注册请求，等待鉴权或注册应答
        /// </summary>
        Authenticating = 2,
        /// <summary>
        /// 已在信令服务器注册成功并持有会话标识
        /// </summary>
        Registered = 3,
        /// <summary>
        /// 正在与对端进行 NAT 打洞
        /// </summary>
        HolePunching = 4,
        /// <summary>
        /// 与对端走中继转发
        /// </summary>
        Relaying = 5,
        /// <summary>
        /// 无需信令服务器的可用状态（未配置服务器地址时由 ConnectAsync 置位）
        /// </summary>
        Connected = 6,
        /// <summary>
        /// 空闲，无进行中的会话
        /// </summary>
        Idle = 7,
        /// <summary>
        /// 与信令服务器或对端断开
        /// </summary>
        Disconnected = 8,
        /// <summary>
        /// 连接、注册或打洞失败
        /// </summary>
        Error = 9
    }
}