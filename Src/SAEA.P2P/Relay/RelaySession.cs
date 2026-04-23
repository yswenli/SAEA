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
*修改时间：2026/04/20 16:10:37
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RelaySession接口
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Relay
{
    public class RelaySession
    {
        public string SessionId { get; set; }
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public RelayState State { get; set; } = RelayState.Pending;
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;
        public long BytesTransferred { get; set; }
        public long MaxQuota { get; set; }
        
        public bool IsExpired(int timeoutMs)
        {
            return (DateTime.UtcNow - LastActiveTime).TotalMilliseconds > timeoutMs;
        }
        
        public bool IsOverQuota => BytesTransferred > MaxQuota;
        
        public void AddBytes(long count)
        {
            BytesTransferred += count;
            LastActiveTime = DateTime.UtcNow;
        }
        
        public void Activate()
        {
            State = RelayState.Active;
            LastActiveTime = DateTime.UtcNow;
        }
        
        public void Close()
        {
            State = RelayState.Closed;
        }
    }
    
    public enum RelayState
    {
        Pending = 0,
        Active = 1,
        Idle = 2,
        Closed = 3
    }
}