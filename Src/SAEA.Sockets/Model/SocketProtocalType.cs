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
*文件名： SocketProtocalType
*版本号： v26.4.23.1
*唯一标识：9cc4e942-848f-4b42-832d-f02583f69ae6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/25 14:41:36
*描述：SocketProtocalType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 帧类型：写在每帧协议头第 9 个字节
    /// </summary>
    public enum SocketProtocalType
    {
        /// <summary>
        /// 心跳帧，body 长度约定为 0，解码器据此投递心跳回调
        /// </summary>
        Heart = 1,

        /// <summary>
        /// 普通请求数据帧，ParseRequest 生成的帧使用该类型
        /// </summary>
        RequestSend = 2,
        /// <summary>
        /// 大帧/文件分块帧，解码器改走文件回调而不是普通数据回调
        /// </summary>
        BigData = 3,

        /// <summary>
        /// 允许接收，作为文件传输前的许可应答
        /// </summary>
        AllowReceive = 5,
        /// <summary>
        /// 拒绝接收，作为文件传输前的拒绝应答
        /// </summary>
        RefuseReceive = 6,

        /// <summary>
        /// 聊天消息帧，UDP 快捷发送的默认类型
        /// </summary>
        ChatMessage = 7,

        /// <summary>
        /// Ping 探测帧
        /// </summary>
        Ping = 8,
        /// <summary>
        /// Pong 应答帧
        /// </summary>
        Pong = 9,


        /// <summary>
        /// 会话关闭帧
        /// </summary>
        Close = 10

    }
}