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
*唯一标识：3ac542ee-5950-4fc2-8d7c-6ce708b2ee13
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/02/10 15:16:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
namespace SAEA.Audio.Model
{
    public enum ProtocalType : byte
    {
        Ping = 0,
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
