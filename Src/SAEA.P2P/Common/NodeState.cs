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
*命名空间：SAEA.P2P.Common
*文件名： NodeState
*版本号： v26.4.23.1
*唯一标识：8524e9da-a611-4750-9f9a-dfcd4504a2c1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：NodeState状态枚举
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 15:38:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：NodeState状态枚举
*
*****************************************************************************/
namespace SAEA.P2P.Common
{
    public enum NodeState
    {
        Init = 0,
        Connecting = 1,
        Authenticating = 2,
        Registered = 3,
        HolePunching = 4,
        Relaying = 5,
        Connected = 6,
        Idle = 7,
        Disconnected = 8,
        Error = 9
    }
}