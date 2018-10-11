/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： Class1
*版本号： V2.1.5.2
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V2.1.5.2
*描述：
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
