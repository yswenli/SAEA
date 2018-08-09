using System;
using System.Collections.Generic;
using System.Text;
using SAEA.Common;

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

        public bool Role
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

        public string Slots
        {
            get; set;
        }

        public string MasterNodeID
        {
            get; set;
        }

        public static List<ClusterNode> ParseList(string info)
        {
            var lines = info.Replace("\r\n", "").Split("\n", StringSplitOptions.RemoveEmptyEntries);

            List<ClusterNode> list = new List<ClusterNode>();

            foreach (var item in lines)
            {
                list.Add(Parse(item));
            }

            return list;
        }

        public static ClusterNode Parse(string info)
        {
            ClusterNode clusterNode = new ClusterNode();

            var arr = info.Split(" ");

            clusterNode.NodeID = arr[0];
            clusterNode.IPPort = arr[1];
            clusterNode.IsMaster = arr[2].Contains("master");

            if (clusterNode.IsMaster)
            {
                clusterNode.Status = arr[7];
                clusterNode.Slots = arr[8];
            }
            else
            {
                clusterNode.MasterNodeID = arr[3];
                clusterNode.Status = arr[7];
            }

            return clusterNode;
        }


    }
}
