/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Type
*文件名： QueueSocketMsgType
*版本号： V3.3.3.1
*唯一标识：dae51812-f614-4e6c-95d4-bd1b0e4aea80
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 17:51:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 17:51:32
*修改人： yswenli
*版本号： V3.3.3.1
*描述：
*
*****************************************************************************/

namespace SAEA.QueueSocket.Type
{
    public enum QueueSocketMsgType : byte
    {
        Ping = 1,
        Pong = 2,
        Publish = 3,
        Subcribe = 4,
        Unsubcribe = 5,
        Close = 6,
        Data = 7
    }
}
