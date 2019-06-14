/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisCluster
*版本号： v4.5.6.7
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
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket
{
    /// <summary>
    /// redis cluster相关操作
    /// </summary>
    public partial class RedisClient
    {
        /// <summary>
        /// redis cluster中重置连接事件
        /// </summary>
        /// <param name="ipPort"></param>
        /// <returns></returns>
        private RedisConnection _redisDataBase_OnRedirect(string ipPort)
        {
            var cnn = RedisConnectionManager.Get(ipPort);

            if (cnn != null)
            {
                return (RedisConnection)cnn;
            }
            else
            {
                this.IsConnected = false;
                this.RedisConfig = new RedisConfig(ipPort, this.RedisConfig.Passwords, this.RedisConfig.ActionTimeOut);

                if (_debugModel)
                {
                    _cnn = new RedisConnectionDebug(RedisConfig.GetIPPort(), this.RedisConfig.ActionTimeOut);
                }
                else
                {
                    _cnn = new RedisConnection(RedisConfig.GetIPPort(), this.RedisConfig.ActionTimeOut);
                }
                
                _cnn.OnRedirect += _redisConnection_OnRedirect;
                _cnn.OnDisconnected += _cnn_OnDisconnected;
                this.Connect();
                RedisConnectionManager.Set(ipPort, _cnn);
                return _cnn;
            }
        }

        /// <summary>
        /// redis cluster中重置连接事件
        /// </summary>
        /// <param name="ipPort"></param>
        /// <param name="operationType"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Interface.IResult _redisConnection_OnRedirect(string ipPort, OperationType operationType, params object[] args)
        {
            var cnn = RedisConnectionManager.Get(ipPort);

            if (cnn == null)
            {
                this.IsConnected = false;
                this.RedisConfig = new RedisConfig(ipPort, this.RedisConfig.Passwords, this.RedisConfig.ActionTimeOut);
                if (_debugModel)
                {
                    cnn = new RedisConnectionDebug(RedisConfig.GetIPPort(), this.RedisConfig.ActionTimeOut);
                }
                else
                {
                    cnn = new RedisConnection(RedisConfig.GetIPPort(), this.RedisConfig.ActionTimeOut);
                }
                cnn.OnRedirect += _redisConnection_OnRedirect;
                cnn.OnDisconnected += _cnn_OnDisconnected;
                this.Connect();
                RedisConnectionManager.Set(ipPort, cnn);
            }

            _cnn = cnn;


            switch (operationType)
            {
                case OperationType.Do:
                    return _cnn.Do((RequestType)args[0]);
                case OperationType.DoBatchWithDic:
                    return _cnn.DoBatchWithDic((RequestType)args[0], (Dictionary<string, string>)args[1]);
                case OperationType.DoBatchWithIDDic:
                    return _cnn.DoBatchWithIDDic((RequestType)args[0], (string)args[1], (Dictionary<double, string>)args[2]);
                case OperationType.DoBatchWithIDKeys:
                    return _cnn.DoBatchWithIDKeys((RequestType)args[0], (string)args[1], (string[])args[3]);
                case OperationType.DoBatchWithParams:
                    return _cnn.DoBatchWithParams((RequestType)args[0], (string[])args[1]);
                case OperationType.DoCluster:
                    return _cnn.DoCluster((RequestType)args[0], (object[])args[1]);
                case OperationType.DoClusterSetSlot:
                    return _cnn.DoClusterSetSlot((RequestType)args[0], (string)args[1], (int)args[2], (string)args[3]);
                case OperationType.DoExpire:
                    _cnn.DoExpire((string)args[0], (int)args[1]);
                    break;
                case OperationType.DoExpireInsert:
                    _cnn.DoExpireInsert((RequestType)args[0], (string)args[1], (string)args[2], (int)args[3]);
                    break;
                case OperationType.DoHash:
                    return _cnn.DoHash((RequestType)args[0], (string)args[1], (string)args[2], (string)args[3]);
                case OperationType.DoInOne:
                    return _cnn.DoInOne((RequestType)args[0], (string)args[1]);
                case OperationType.DoRang:
                    return _cnn.DoRang((RequestType)args[0], (string)args[1], (double)args[2], (double)args[3]);
                case OperationType.DoScan:
                    return _cnn.DoScan((RequestType)args[0], (int)args[1], (string)args[2], (int)args[3]);
                case OperationType.DoScanKey:
                    return _cnn.DoScanKey((RequestType)args[0], (string)args[1], (int)args[2], (string)args[3], (int)args[4]);
                case OperationType.DoSub:
                    _cnn.DoSub((string[])args[0], (Action<string, string>)args[1]);
                    break;
                case OperationType.DoWithKey:
                    return _cnn.DoWithKey((RequestType)args[0], (string)args[1]);
                case OperationType.DoWithKeyValue:
                    return _cnn.DoWithKeyValue((RequestType)args[0], (string)args[1], (string)args[2]);
                default:
                    return null;
            }
            return null;

        }

        /// <summary>
        /// 建立redis cluster 本地映射
        /// </summary>
        /// <param name="ipPort"></param>
        private void GetClusterMap(string ipPort)
        {
            var clusterNodes = ClusterNodes;

            RedisConnectionManager.SetClusterNodes(clusterNodes);

            foreach (var item in clusterNodes)
            {
                if (!RedisConnectionManager.Exsits(item.IPPort))
                {
                    TaskHelper.Start(() =>
                    {
                        var cnn = new RedisConnection(item.IPPort);
                        cnn.OnRedirect += _redisConnection_OnRedirect;
                        cnn.OnDisconnected += _cnn_OnDisconnected;
                        cnn.Connect();
                        cnn.RedisServerType = item.IsMaster ? RedisServerType.ClusterMaster : RedisServerType.ClusterSlave;
                        RedisConnectionManager.Set(item.IPPort, cnn);
                    });
                }
            }
        }


        /// <summary>
        /// 是否是cluster node
        /// </summary>
        public bool IsCluster
        {
            get
            {
                var info = Info("Cluster");
                if (info.Contains("cluster_enabled:1"))
                {
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 集群信息
        /// </summary>
        /// <returns></returns>
        public string ClusterInfoStr()
        {
            return _cnn.DoCluster(RequestType.CLUSTER_INFO).Data;
        }
        /// <summary>
        /// 集群信息
        /// </summary>
        public ClusterInfo ClusterInfo
        {
            get
            {
                var data = ClusterInfoStr();

                return ClusterInfo.Parse(data);
            }
        }
        /// <summary>
        /// 集群节点信息
        /// </summary>
        /// <returns></returns>
        public string ClusterNodesStr()
        {
            return _cnn.DoCluster(RequestType.CLUSTER_NODES).Data;
        }
        /// <summary>
        /// 集群节点信息
        /// </summary>
        public List<ClusterNode> ClusterNodes
        {
            get
            {
                var data = ClusterNodesStr();

                return ClusterNode.ParseList(data);
            }
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool AddNode(string ip, int port)
        {
            return _cnn.DoCluster(RequestType.CLUSTER_MEET, ip, port).Data == OK;
        }
        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public bool DeleteNode(string nodeID)
        {
            return _cnn.DoCluster(RequestType.CLUSTER_FORGET, nodeID).Data == OK;
        }

        /// <summary>
        /// 将当前节点改成指定节点的从节点
        /// </summary>
        /// <param name="masterNodeID"></param>
        /// <returns></returns>
        public bool Replicate(string masterNodeID)
        {
            return _cnn.DoCluster(RequestType.CLUSTER_REPLICATE, masterNodeID).Data == OK;
        }

        /// <summary>
        /// 当前节点添加slots
        /// </summary>
        /// <param name="slots"></param>
        /// <returns></returns>
        public bool AddSlots(params int[] slots)
        {
            return _cnn.DoCluster(RequestType.CLUSTER_ADDSLOTS, slots).Data == OK;
        }

        /// <summary>
        /// 当前节点移除slots
        /// </summary>
        /// <param name="slots"></param>
        /// <returns></returns>
        public bool DelSlots(params int[] slots)
        {
            return _cnn.DoCluster(RequestType.CLUSTER_DELSLOTS, slots).Data == OK;
        }
        /// <summary>
        /// 当前节点移除全部slots
        /// </summary>
        /// <returns></returns>
        public bool FlushSlots()
        {
            return _cnn.DoCluster(RequestType.CLUSTER_FLUSHSLOTS).Data == OK;
        }
        /// <summary>
        /// 将当前节点的slot 指派给 node_id指定的节点，如果槽已经指派给另一个节点，那么先让另一个节点删除该槽，然后再进行指派。
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public bool SetSlot(int slot, string nodeID)
        {
            return _cnn.DoClusterSetSlot(RequestType.CLUSTER_SETSLOT, "NODE", slot, nodeID).Data == OK;
        }
        /// <summary>
        /// 将slot 指派给 node_id指定的节点，如果槽已经指派给另一个节点，那么先让另一个节点删除该槽，然后再进行指派。
        /// </summary>
        /// <param name="slots"></param>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public void SetSlots(int[] slots, string nodeID)
        {
            foreach (var slot in slots)
            {
                SetSlot(slot, nodeID);
            }
        }

        /// <summary>
        /// 将当前节点的slot 迁移到 node_id 指定的节点中
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public bool Migrating(int slot, string nodeID)
        {
            return _cnn.DoClusterSetSlot(RequestType.CLUSTER_SETSLOT, "MIGRATING", slot, nodeID).Data == OK;
        }

        /// <summary>
        /// 将当前节点的slot 迁移到 node_id 指定的节点中
        /// </summary>
        /// <param name="slots"></param>
        /// <param name="nodeID"></param>
        public void Migratings(int[] slots, string nodeID)
        {
            foreach (var slot in slots)
            {
                SetSlot(slot, nodeID);
            }
        }

        /// <summary>
        /// 从指定的 node_id 节点中导入 slot 到本节点
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public bool Importing(int slot, string nodeID)
        {
            return _cnn.DoClusterSetSlot(RequestType.CLUSTER_SETSLOT, "IMPORTING", slot, nodeID).Data == OK;
        }

        /// <summary>
        /// 从指定的 node_id 节点中导入 slot 到本节点
        /// </summary>
        /// <param name="slots"></param>
        /// <param name="nodeID"></param>
        public void Importings(int[] slots, string nodeID)
        {
            foreach (var slot in slots)
            {
                SetSlot(slot, nodeID);
            }
        }

        /// <summary>
        /// 取消 slot 的导入（import）或者迁移（migrate）
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public bool Stable(int slot, string nodeID)
        {
            return _cnn.DoClusterSetSlot(RequestType.CLUSTER_SETSLOT, "STABLE", slot, nodeID).Data == OK;
        }

        /// <summary>
        /// 计算键 key 应该被放置在哪个槽上
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int KeySlot(string key)
        {
            int slot = -1;
            int.TryParse(_cnn.DoCluster(RequestType.CLUSTER_KEYSLOT, key).Data, out slot);
            return slot;
        }

        /// <summary>
        /// 计算一个slot 包含多少个key
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public int CountKeysInSlot(int slot)
        {
            int count = -1;
            int.TryParse(_cnn.DoCluster(RequestType.CLUSTER_COUNTKEYSINSLOT, slot).Data, out count);
            return count;
        }

        /// <summary>
        /// 返回一个slot中count个key的集合
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public List<string> GetKeysInSlot(int slot)
        {
            return _cnn.DoCluster(RequestType.CLUSTER_GETKEYSINSLOT, slot).ToList<string>();
        }

        /// <summary>
        /// 将节点的配置保存到配置文件
        /// </summary>
        /// <returns></returns>
        public bool SaveClusterConfig()
        {
            return _cnn.DoCluster(RequestType.CLUSTER_SAVECONFIG).Data == OK;
        }
    }
}
