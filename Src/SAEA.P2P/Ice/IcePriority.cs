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
*命名空间：SAEA.P2P.Ice
*文件名： IcePriority
*版本号： v26.11.5.1
*唯一标识：0e71a5ea-082c-4d56-9d2f-3c8622ac2316
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:00:00
*描述：ICE 候选与候选对优先级计算
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：候选对优先级改为 64 位无符号整数
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 优先级计算：候选优先级与候选对优先级
    /// </summary>
    public static class IcePriority
    {
        /// <summary>
        /// UDP 候选的本地偏好，取 16 位最大值
        /// </summary>
        public const uint UdpLocalPreference = 65535;

        /// <summary>
        /// TCP 主动方向偏好
        /// </summary>
        public const int TcpDirectionActive = 6;

        /// <summary>
        /// TCP 被动方向偏好
        /// </summary>
        public const int TcpDirectionPassive = 4;

        /// <summary>
        /// TCP 同时打开方向偏好
        /// </summary>
        public const int TcpDirectionSimultaneousOpen = 2;

        /// <summary>
        /// 按类型优先级、本地偏好与分量编号计算候选优先级
        /// </summary>
        /// <param name="type">候选类型，其数值即类型优先级</param>
        /// <param name="localPreference">本地偏好，不得超过 65535</param>
        /// <param name="componentId">分量编号，取值 1 到 256</param>
        /// <returns>候选优先级</returns>
        /// <exception cref="ArgumentOutOfRangeException">分量编号越界或本地偏好超过 65535</exception>
        public static uint Compute(CandidateType type, uint localPreference, int componentId)
        {
            if (componentId < 1 || componentId > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(componentId), "分量编号必须在 1 到 256 之间");
            }
            if (localPreference > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(localPreference), "本地偏好不能超过 65535");
            }
            return ((uint)type << 24) + (localPreference << 8) + (uint)(256 - componentId);
        }

        /// <summary>
        /// 计算 UDP 候选的优先级，本地偏好取最大值
        /// </summary>
        /// <param name="type">候选类型，其数值即类型优先级</param>
        /// <param name="componentId">分量编号，取值 1 到 256</param>
        /// <returns>候选优先级</returns>
        public static uint ComputeForUdp(CandidateType type, int componentId)
        {
            return Compute(type, UdpLocalPreference, componentId);
        }

        /// <summary>
        /// 计算 TCP 候选的优先级，本地偏好按候选方向类型折算
        /// </summary>
        /// <param name="type">候选类型，其数值即类型优先级</param>
        /// <param name="componentId">分量编号，取值 1 到 256</param>
        /// <param name="tcpType">TCP 方向类型，不得为 None</param>
        /// <returns>候选优先级</returns>
        /// <exception cref="ArgumentException">TCP 方向类型为 None</exception>
        public static uint ComputeForTcp(CandidateType type, int componentId, TcpCandidateType tcpType)
        {
            if (tcpType == TcpCandidateType.None)
            {
                throw new ArgumentException("TCP 候选必须指定方向类型", nameof(tcpType));
            }

            int directionPreference;
            if (tcpType == TcpCandidateType.Active)
            {
                directionPreference = TcpDirectionActive;
            }
            else if (tcpType == TcpCandidateType.Passive)
            {
                directionPreference = TcpDirectionPassive;
            }
            else
            {
                directionPreference = TcpDirectionSimultaneousOpen;
            }

            // 方向偏好整体上移到第 13 位，低位留作后续差异化扩展，因此这里恒为 0
            uint localPreference = (uint)((1 << 13) * directionPreference);
            return Compute(type, localPreference, componentId);
        }

        /// <summary>
        /// 按 RFC 8445 §6.1.2.3 的 G/D 公式计算候选对优先级
        /// </summary>
        /// <param name="localPriority">本地候选优先级</param>
        /// <param name="remotePriority">远端候选优先级</param>
        /// <param name="isControlling">本地是否为控制方；为 true 时本地候选是 G</param>
        /// <returns>64 位候选对优先级</returns>
        public static ulong ComputePairPriority(uint localPriority, uint remotePriority, bool isControlling)
        {
            uint g = isControlling ? localPriority : remotePriority;
            uint d = isControlling ? remotePriority : localPriority;

            // MIN 项左移 32 位是主导项，必须保留高位，因此结果不能截断成 32 位
            return ((ulong)Math.Min(g, d) << 32) | ((ulong)Math.Max(g, d) << 1) | (g > d ? 1UL : 0UL);
        }

        /// <summary>
        /// 取候选类型自身的类型优先级
        /// </summary>
        /// <param name="type">候选类型</param>
        /// <returns>类型优先级</returns>
        public static int GetTypePreference(CandidateType type)
        {
            return (int)type;
        }
    }
}
