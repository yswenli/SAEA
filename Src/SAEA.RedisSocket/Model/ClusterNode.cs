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
*唯一标识：2284212d-91e5-4504-9a96-84b6c4346bad
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/07/31 19:50:00
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/07/31 19:50:00
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// ClusterNode
    /// </summary>
    public class ClusterNode
    {
        public string NodeID
        {
            get; set;
        }

        public string IPPort
        {
            get; set;
        }

        public string Status
        {
            get; set;
        }

        public bool IsMaster
        {
            get; set;
        }

        public int MinSlots
        {
            get; set;
        }
        public int MaxSlots
        {
            get; set;
        }

        public string MasterNodeID
        {
            get; set;
        }

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