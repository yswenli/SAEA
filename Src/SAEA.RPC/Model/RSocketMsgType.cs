/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Model
*文件名： QueueSocketMsgType
*版本号： v4.2.3.1
*唯一标识：166dedf9-8f2c-4d4e-ad04-56a1acb2b307
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 15:18:12
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 15:18:12
*修改人： yswenli
*版本号： v4.2.3.1
*描述：
*
*****************************************************************************/

namespace SAEA.RPC.Model
{
    /// <summary>
    /// RPC消息类型
    /// </summary>
    public enum RSocketMsgType : byte
    {
        Ping = 1,
        Pong = 2,
        Request = 3,
        Response = 4,
        Error = 41,
        Close = 5
    }
}
