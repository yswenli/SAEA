/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
namespace SAEA.WebSocket.Type
{
    /// <summary>
    /// ws Opcode 类型
    /// </summary>
    public enum WSProtocalType : byte
    {
        /// <summary>
        /// 表示一个中间数据包
        /// </summary>
        Cont = 0,
        /// <summary>
        /// 表示一个text类型数据包
        /// </summary>
        Text = 1,
        /// <summary>
        /// 表示一个binary类型数据包
        /// </summary>
        Binary = 2,
        /// <summary>
        /// 表示一个断开连接类型数据包
        /// </summary>
        Close = 8,
        /// <summary>
        /// 表示一个ping类型数据包
        /// </summary>
        Ping = 9,
        /// <summary>
        /// 表示一个pong类型数据包
        /// </summary>
        Pong = 10
    }
}
