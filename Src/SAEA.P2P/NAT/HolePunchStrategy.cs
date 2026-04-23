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
*命名空间：SAEA.P2P.NAT
*文件名： HolePunchStrategy
*版本号： v26.4.23.1
*唯一标识：3ce7a973-da2e-43c0-8053-cec6daf38b35
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：HolePunchStrategy策略枚举
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 15:38:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HolePunchStrategy策略枚举
*
*****************************************************************************/
namespace SAEA.P2P.NAT
{
    public enum HolePunchStrategy
    {
        PreferDirect = 0,
        PreferRelay = 1,
        DirectOnly = 2,
        RelayOnly = 3
    }
}