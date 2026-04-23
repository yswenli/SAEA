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
*命名空间：SAEA.QueueSocket.Type
*文件名： QueueSocketMsgType
*版本号： v26.4.23.1
*唯一标识：a991172f-4007-4062-b094-6c99f200c104
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：QueueSocketMsgType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：QueueSocketMsgType类型枚举
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