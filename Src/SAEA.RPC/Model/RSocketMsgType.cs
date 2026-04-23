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
*修改时间：2018/05/25 17:28:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RSocketMsgType类型枚举
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

        RegistNotice = 31,
        Notice = 41,

        Error = 42,
        Close = 5
    }
}