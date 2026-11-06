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
*命名空间：SAEA.MessageSocket.Model
*文件名： MessageContext
*版本号： v26.4.23.1
*唯一标识：23fe0d8a-d707-429a-8be8-508d3888d202
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：MessageContext上下文类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:17
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.MessageSocket.Model
{
    /// <summary>
    /// 消息通信上下文，持有会话用户令牌与消息解码器，客户端与服务端共用
    /// </summary>
    public class MessageContext : BaseContext<BaseCoder>
    {
        /// <summary>
        /// 当前会话的用户令牌，含登录状态与最近活动时间
        /// </summary>
        public override IUserToken UserToken { get; set; }

        /// <summary>
        /// 消息解码器，用于从接收数据中拆出完整协议帧
        /// </summary>
        public override ICoder Unpacker { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public MessageContext()
        {
            this.UserToken = new MessageUserToken();
            this.Unpacker = new BaseCoder();
            this.UserToken.Coder = this.Unpacker;
        }
    }
}