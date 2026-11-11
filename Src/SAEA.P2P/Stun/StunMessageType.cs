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
*命名空间：SAEA.P2P.Stun
*文件名： StunMessageType
*版本号： v26.11.5.1
*唯一标识：9a1ca6e2-6ce3-4c5e-81a8-ee19c6f76544
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：STUN 方法枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 方法枚举
*
*****************************************************************************/
namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 方法；数值是消息类型编码里的方法字段原值，不能当成完整的消息类型使用
    /// </summary>
    public enum StunMessageType
    {
        /// <summary>
        /// 绑定，用于连通性检查与映射地址发现
        /// </summary>
        Binding = 0x001,

        /// <summary>
        /// TURN 分配请求
        /// </summary>
        Allocate = 0x003,

        /// <summary>
        /// TURN 刷新分配
        /// </summary>
        Refresh = 0x004,

        /// <summary>
        /// TURN 发送指示
        /// </summary>
        Send = 0x006,

        /// <summary>
        /// TURN 数据指示
        /// </summary>
        Data = 0x007,

        /// <summary>
        /// TURN 创建许可
        /// </summary>
        CreatePermission = 0x008,

        /// <summary>
        /// TURN 通道绑定
        /// </summary>
        ChannelBind = 0x009,

        /// <summary>
        /// TURN over TCP 的连接请求
        /// </summary>
        Connect = 0x00A,

        /// <summary>
        /// TURN over TCP 的连接绑定
        /// </summary>
        ConnectionBind = 0x00B
    }
}