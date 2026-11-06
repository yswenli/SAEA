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
*命名空间：SAEA.RPC.Model
*文件名： RSocketMsgType
*版本号： v26.4.23.1
*唯一标识：87ce5cb8-e2f0-4d49-9258-7a003da2e4cc
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：RSocketMsgType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:32
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.RPC.Model
{
    /// <summary>
    /// RPC消息类型
    /// </summary>
    public enum RSocketMsgType : byte
    {
        /// <summary>
        /// 心跳请求
        /// </summary>
        Ping = 1,
        /// <summary>
        /// 心跳响应
        /// </summary>
        Pong = 2,

        /// <summary>
        /// 服务调用请求
        /// </summary>
        Request = 3,
        /// <summary>
        /// 服务调用响应
        /// </summary>
        Response = 4,

        /// <summary>
        /// 注册推送通知
        /// </summary>
        RegistNotice = 31,
        /// <summary>
        /// 服务端推送通知
        /// </summary>
        Notice = 41,

        /// <summary>
        /// 错误响应
        /// </summary>
        Error = 42,
        /// <summary>
        /// 关闭连接
        /// </summary>
        Close = 5
    }
}