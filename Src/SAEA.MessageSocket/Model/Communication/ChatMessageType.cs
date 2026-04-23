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
*版本号： v26.4.23.1
*唯一标识：e829334c-0e2d-4677-ad05-fba258d0d415
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ChatMessageType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ChatMessageType类型枚举
*
*****************************************************************************/
namespace SAEA.MessageSocket.Model.Communication
{
    enum ChatMessageType
    {
        Login = 1,
        LoginAnswer = 2,

        Subscribe = 3,
        SubscribeAnswer = 4,

        UnSubscribe = 5,
        UnSubscribeAnswer = 6,

        ChannelMessage = 7,

        PrivateMessage = 8,
        PrivateMessageAnswer = 9,

        CreateGroup = 10,
        CreateGroupAnswer = 11,

        AddMember = 12,
        AddMemberAnswer = 13,

        RemoveMember = 14,
        RemoveMemberAnswer = 15,

        RemoveGroup = 16,
        RemoveGroupAnswer = 17,

        GroupMessage = 18,
        GroupMessageAnswer = 19,

    }
}