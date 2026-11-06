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
*命名空间：SAEA.RedisSocket.Model
*文件名： ClusterNode
*版本号： v26.4.23.1
*唯一标识：a4354a77-a06e-47ef-b2e4-3098eb3dd653
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/07/31 19:50:00
*描述：ClusterNode类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 集群节点信息，对应 CLUSTER NODES 输出中的一行
    /// </summary>
    public class ClusterNode
    {
        /// <summary>
        /// 节点 ID，集群内唯一
        /// </summary>
        public string NodeID
        {
            get; set;
        }

        /// <summary>
        /// 节点的地址与端口，格式为 ip:port
        /// </summary>
        public string IPPort
        {
            get; set;
        }

        /// <summary>
        /// 节点状态标记，如 connected、disconnected
        /// </summary>
        public string Status
        {
            get; set;
        }

        /// <summary>
        /// 是否为 master 节点
        /// </summary>
        public bool IsMaster
        {
            get; set;
        }

        /// <summary>
        /// 该节点负责的最小槽位，从节点继承其主节点的范围
        /// </summary>
        public int MinSlots
        {
            get; set;
        }
        /// <summary>
        /// 该节点负责的最大槽位，从节点继承其主节点的范围
        /// </summary>
        public int MaxSlots
        {
            get; set;
        }

        /// <summary>
        /// 所属主节点 ID，仅从节点有值
        /// </summary>
        public string MasterNodeID
        {
            get; set;
        }

        /// <summary>
        /// 解析 CLUSTER NODES 的全部节点，并把主节点的槽位范围同步给对应从节点
        /// </summary>
        /// <param name="info">CLUSTER NODES 的原始返回内容，每行一个节点</param>
        /// <returns>节点列表，解析异常时返回 null</returns>
        public static List<ClusterNode> ParseList(string info)
        {
            try
            {
                var lines = info.Replace("\r\n", "").Split("\n", StringSplitOptions.RemoveEmptyEntries);

                List<ClusterNode> list = new List<ClusterNode>();

                foreach (var item in lines)
                {
                    list.Add(Parse(item));
                }

                foreach (var item in list)
                {
                    if (!item.IsMaster)
                    {
                        var masterNode = list.Where(b => b.NodeID == item.MasterNodeID).FirstOrDefault();
                        if (masterNode != null)
                        {
                            item.MinSlots = masterNode.MinSlots;
                            item.MaxSlots = masterNode.MaxSlots;
                        }
                    }
                }

                return list;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 解析 CLUSTER NODES 中的单个节点行
        /// </summary>
        /// <param name="info">单行节点信息，以空格分隔各字段</param>
        /// <returns>解析后的节点，字段缺失时返回仅有部分属性值的节点</returns>
        public static ClusterNode Parse(string info)
        {
            ClusterNode clusterNode = new ClusterNode();

            try
            {
                var arr = info.Split(" ");

                clusterNode.NodeID = arr[0];
                clusterNode.IPPort = arr[1];
                clusterNode.IsMaster = arr[2].Contains("master");

                if (clusterNode.IsMaster)
                {
                    clusterNode.Status = arr[7];

                    if (arr.Length >= 9)
                    {
                        var sarr = arr[8].Split("-");
                        try
                        {
                            clusterNode.MinSlots = int.Parse(sarr[0]);
                        }
                        catch { }
                        try
                        {
                            clusterNode.MaxSlots = int.Parse(sarr[1]);
                        }
                        catch { }                        
                    }
                }
                else
                {
                    clusterNode.MasterNodeID = arr[3];
                    clusterNode.Status = arr[7];
                }
            }
            catch
            {

            }
            return clusterNode;
        }


    }
}