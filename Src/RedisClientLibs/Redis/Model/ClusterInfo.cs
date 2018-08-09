/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Model
*文件名： ClusterInfo
*版本号： V1.0.0.0
*唯一标识：23cf910b-3bed-4d80-9e89-92c04fba1e5e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/7/25 20:12:40
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/7/25 20:12:40
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using SAEA.Common;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// ClusterInfo
    /// </summary>
    public class ClusterInfo
    {
        /// <summary>
        /// 群集状态
        /// </summary>
        public string cluster_state
        {
            get; set;
        }
        /// <summary>
        /// 已分配的集群slots
        /// </summary>
        public int cluster_slots_assigned
        {
            get; set;
        }
        /// <summary>
        /// 正在使用集群slots
        /// </summary>
        public int cluster_slots_ok
        {
            get; set;
        }
        /// <summary>
        /// 即将失效的集群slots
        /// </summary>
        public int cluster_slots_pfail
        {
            get; set;
        }
        /// <summary>
        /// 已失效果集群slots
        /// </summary>
        public int cluster_slots_fail
        {
            get; set;
        }
        /// <summary>
        /// 集群节点数
        /// </summary>
        public int cluster_known_nodes
        {
            get; set;
        }
        /// <summary>
        /// 集群大小
        /// </summary>
        public int cluster_size
        {
            get; set;
        }
        /// <summary>
        /// 当前集群时代
        /// </summary>
        public int cluster_current_epoch
        {
            get; set;
        }
        /// <summary>
        /// 我的集群时代
        /// </summary>
        public int cluster_my_epoch
        {
            get; set;
        }
        /// <summary>
        /// 集群状态消息发送量
        /// </summary>
        public long cluster_stats_messages_sent
        {
            get; set;
        }
        /// <summary>
        /// 集群状态消息接收量
        /// </summary>
        public long cluster_stats_messages_received
        {
            get; set;
        }

        public static ClusterInfo Parse(string info)
        {
            var lines = info.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            var clusterInfo = new ClusterInfo()
            {
                cluster_state = lines[0].Split(":")[1],
                cluster_slots_assigned = int.Parse(lines[1].Split(":")[1]),
                cluster_slots_ok = int.Parse(lines[2].Split(":")[1]),
                cluster_slots_pfail = int.Parse(lines[3].Split(":")[1]),
                cluster_slots_fail = int.Parse(lines[4].Split(":")[1]),
                cluster_known_nodes = int.Parse(lines[5].Split(":")[1]),
                cluster_size = int.Parse(lines[6].Split(":")[1]),
                cluster_current_epoch = int.Parse(lines[7].Split(":")[1]),
                cluster_my_epoch = int.Parse(lines[8].Split(":")[1]),
                cluster_stats_messages_sent = int.Parse(lines[9].Split(":")[1]),
                cluster_stats_messages_received = int.Parse(lines[10].Split(":")[1]),
            };

            return clusterInfo;
        }
    }
}
