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
*命名空间：SAEA.MessageSocket.Model.Communication
*文件名： ChatMessageType
*版本号： v26.11.5.1
*唯一标识：e829334c-0e2d-4677-ad05-fba258d0d415
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ChatMessageType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：改为公开类型并补全 XML 注释，供处理器链下游按协议类型分支
*
*****************************************************************************/
namespace SAEA.MessageSocket.Model.Communication
{
    /// <summary>
    /// 消息信封的协议类型，客户端请求与服务端应答成对出现
    /// </summary>
    public enum ChatMessageType
    {
        /// <summary>
        /// 登录请求
        /// </summary>
        Login = 1,

        /// <summary>
        /// 登录应答
        /// </summary>
        LoginAnswer = 2,

        /// <summary>
        /// 频道订阅请求，内容为频道名
        /// </summary>
        Subscribe = 3,

        /// <summary>
        /// 频道订阅应答，内容为 "1" 表示成功
        /// </summary>
        SubscribeAnswer = 4,

        /// <summary>
        /// 取消频道订阅请求，内容为频道名
        /// </summary>
        UnSubscribe = 5,

        /// <summary>
        /// 取消频道订阅应答，内容为 "1" 表示成功
        /// </summary>
        UnSubscribeAnswer = 6,

        /// <summary>
        /// 频道消息，内容为序列化后的频道消息对象
        /// </summary>
        ChannelMessage = 7,

        /// <summary>
        /// 私聊消息，内容为序列化后的私聊消息对象
        /// </summary>
        PrivateMessage = 8,

        /// <summary>
        /// 私聊消息应答
        /// </summary>
        PrivateMessageAnswer = 9,

        /// <summary>
        /// 创建群组请求，内容为群名
        /// </summary>
        CreateGroup = 10,

        /// <summary>
        /// 创建群组应答，内容为 "1" 表示成功
        /// </summary>
        CreateGroupAnswer = 11,

        /// <summary>
        /// 加入群组请求，内容为群名
        /// </summary>
        AddMember = 12,

        /// <summary>
        /// 加入群组应答，内容为 "1" 表示成功
        /// </summary>
        AddMemberAnswer = 13,

        /// <summary>
        /// 退出群组请求，内容为群名
        /// </summary>
        RemoveMember = 14,

        /// <summary>
        /// 退出群组应答，内容为 "1" 表示成功
        /// </summary>
        RemoveMemberAnswer = 15,

        /// <summary>
        /// 解散群组请求，内容为群名
        /// </summary>
        RemoveGroup = 16,

        /// <summary>
        /// 解散群组应答，内容为 "1" 表示成功
        /// </summary>
        RemoveGroupAnswer = 17,

        /// <summary>
        /// 群组消息，内容为序列化后的群组消息对象
        /// </summary>
        GroupMessage = 18,

        /// <summary>
        /// 群组消息应答
        /// </summary>
        GroupMessageAnswer = 19,
    }
}