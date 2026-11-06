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
*命名空间：SAEA.MessageSocket.Model.Business
*文件名： ChannelMessage
*版本号： v26.4.23.1
*唯一标识：8f575bb9-47d2-471b-9557-ecbff0119d55
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ChannelMessage消息类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:17
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MessageSocket.Model.Business
{
    /// <summary>
    /// 频道消息，包含频道名称、发送者、发送时间与正文
    /// </summary>
    public class ChannelMessage: IMessage
    {
        /// <summary>
        /// 频道名称，客户端发送时必须填写，服务端据此确定转发范围
        /// </summary>
        public string Name
        {
            get;set;
        }

        /// <summary>
        /// 发送者标识，由服务端转发时填为发送者会话 ID
        /// </summary>
        public string Sender
        {
            get;set;
        }

        /// <summary>
        /// 发送时间字符串，由服务端在转发时填写
        /// </summary>
        public string Sended
        {
            get;set;
        }

        /// <summary>
        /// 消息正文
        /// </summary>
        public string Content
        {
            get;set;
        }
    }
}