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
*命名空间：SAEA.Audio.Model
*文件名： ProtocalType
*版本号： v26.4.23.1
*唯一标识：30a45eb6-0841-4820-b54d-f4b5001096e9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：ProtocalType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:33
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.Audio.Model
{
    /// <summary>
    /// 语音协议的消息类型，底层为单字节
    /// </summary>
    public enum ProtocalType : byte
    {
        /// <summary>
        /// 心跳请求
        /// </summary>
        Ping = 0,
        /// <summary>
        /// 心跳应答
        /// </summary>
        Pong = 1,

        /// <summary>
        /// 邀请
        /// </summary>
        Invite = 2,

        /// <summary>
        /// 同意
        /// </summary>
        Agree = 3,
        /// <summary>
        /// 拒绝
        /// </summary>
        Disagree = 4,
        /// <summary>
        /// 数据
        /// </summary>
        Data = 5,
        /// <summary>
        /// 加入
        /// </summary>
        Join = 6,
        /// <summary>
        /// 退出
        /// </summary>
        Quit = 7
    }
}
