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
*命名空间：SAEA.WebSocket.Type
*文件名： WSProtocalType
*版本号： v26.4.23.1
*唯一标识：3214be60-2185-4b32-8745-a41a231fdc76
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
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