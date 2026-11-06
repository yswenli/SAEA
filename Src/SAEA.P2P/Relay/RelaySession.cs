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
*命名空间：SAEA.P2P.Relay
*文件名： RelaySession
*版本号： v26.4.23.1
*唯一标识：974aa1a1-aa43-41b4-afd2-99d21a8e2757
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:10:37
*描述：RelaySession接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Relay
{
    /// <summary>
    /// 中继会话状态：记录两端节点标识、配额、流量统计与空闲超时判定
    /// </summary>
    public class RelaySession
    {
        /// <summary>
        /// 会话唯一标识，由创建方生成
        /// </summary>
        public string SessionId { get; set; }
        /// <summary>
        /// 发起中继的源节点标识
        /// </summary>
        public string SourceNodeId { get; set; }
        /// <summary>
        /// 中继目标的节点标识
        /// </summary>
        public string TargetNodeId { get; set; }
        /// <summary>
        /// 会话当前状态，新建时默认 Pending
        /// </summary>
        public RelayState State { get; set; } = RelayState.Pending;
        /// <summary>
        /// 会话创建时间，UTC
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// 最近一次收发数据或激活的时间，UTC，用于空闲超时判定
        /// </summary>
        public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// 会话累计传输字节数，收发双向均累加
        /// </summary>
        public long BytesTransferred { get; set; }
        /// <summary>
        /// 会话累计字节配额，小于等于 0 表示不限制
        /// </summary>
        public long MaxQuota { get; set; }
        
        /// <summary>
        /// 判断会话空闲是否超过指定时长
        /// </summary>
        /// <param name="timeoutMs">空闲超时阈值，单位毫秒</param>
        /// <returns>最后活跃时间距今超过阈值时为 true</returns>
        public bool IsExpired(int timeoutMs)
        {
            return (DateTime.UtcNow - LastActiveTime).TotalMilliseconds > timeoutMs;
        }
        
        // MaxQuota <= 0 means unlimited.
        /// <summary>
        /// 已传输字节数是否超出配额；配额小于等于 0 时恒为 false
        /// </summary>
        public bool IsOverQuota => MaxQuota > 0 && BytesTransferred > MaxQuota;
        
        /// <summary>
        /// 累加传输字节数并刷新最后活跃时间
        /// </summary>
        /// <param name="count">本次新增的字节数，不校验正负与配额</param>
        public void AddBytes(long count)
        {
            BytesTransferred += count;
            LastActiveTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// 将会话置为 Active 并刷新最后活跃时间
        /// </summary>
        public void Activate()
        {
            State = RelayState.Active;
            LastActiveTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// 将会话置为 Closed，不刷新最后活跃时间
        /// </summary>
        public void Close()
        {
            State = RelayState.Closed;
        }
    }
    
    /// <summary>
    /// 中继会话的生命周期状态
    /// </summary>
    public enum RelayState
    {
        /// <summary>
        /// 会话已创建但尚未激活，此时不转发数据
        /// </summary>
        Pending = 0,
        /// <summary>
        /// 会话已激活，可收发中继数据
        /// </summary>
        Active = 1,
        /// <summary>
        /// 会话空闲；当前实现未在任何路径主动设置该状态
        /// </summary>
        Idle = 2,
        /// <summary>
        /// 会话已关闭
        /// </summary>
        Closed = 3
    }
}