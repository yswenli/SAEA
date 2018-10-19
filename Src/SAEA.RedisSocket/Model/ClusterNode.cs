/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.RedisSocket.Model
*文件名： ClusterNode
*版本号： V2.2.1.1
*唯一标识：3806bd74-f304-42b2-ab04-3e219828fa60
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 16:16:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 16:16:57
*修改人： yswenli
*版本号： V2.2.1.1
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
