/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.TurnServer
*文件名： SystemTurnClock
*版本号： v26.11.5.1
*唯一标识：c8a6b1f2-38b7-4b73-9a9e-2f9f9b3c7a11
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：基于系统 UTC 时间的 TURN 时钟
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增基于系统 UTC 时间的 TURN 时钟
*
*****************************************************************************/
using System;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// 基于系统 UTC 时间的 TURN 时钟
    /// </summary>
    public sealed class SystemTurnClock : ITurnClock
    {
        /// <summary>
        /// 进程内共享的默认实例；无状态，可安全复用
        /// </summary>
        public static readonly SystemTurnClock Instance = new SystemTurnClock();

        /// <summary>
        /// 当前 UTC 时间
        /// </summary>
        public DateTimeOffset UtcNow
        {
            get { return DateTimeOffset.UtcNow; }
        }
    }
}