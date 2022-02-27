/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisCluster
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Interface;
using SAEA.RedisSocket.Model;

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
        /// <param name="operationType"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private IResult _redisConnection_OnRedirect(string ipPort, OperationType operationType, params object[] args)
        {
            var cnn = RedisConnectionManager.Get(ipPort);

            var requestType = (RequestType)args[0];

            switch (requestType)
            {
                case RequestType.GET:
                case RequestType.EXISTS:
                case RequestType.SCAN:
                case RequestType.TTL:
                case RequestType.PTTL:
                case RequestType.TYPE:
                case RequestType.HGET:
                case RequestType.HEXISTS:
                case RequestType.HLEN:
                case RequestType.SMEMBERS:
                case RequestType.SISMEMBER:
                case RequestType.SSCAN:
                case RequestType.HSCAN:
                case RequestType.ZSCAN:
                case RequestType.ZSCORE:
                case RequestType.RANDOMKEY:
                case RequestType.ZLEXCOUNT:
                case RequestType.ZCARD:
                case RequestType.ZCOUNT:
                case RequestType.ZRANGE:
                case RequestType.ZRANGEBYLEX:
                case RequestType.ZRANGEBYSCORE:
                case RequestType.ZRANK:
                case RequestType.ZREVRANGE:
                case RequestType.ZREVRANGEBYSCORE:
                case RequestType.ZREVRANK:
                case RequestType.LINDEX:
                case RequestType.LLEN:

                    var slave = RedisConnectionManager.Get(ipPort, false);

                    if (slave != null)
                    {
                        cnn = slave;
                    }
                    break;
            }

            if (cnn == null || !cnn.IsConnected)
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
                if (cnn.Connect())
                {
                    if (!string.IsNullOrEmpty(this.RedisConfig.Passwords))
                        cnn.Auth(this.RedisConfig.Passwords);
                    _cnn = cnn;
                    this.IsConnected = true;
                    RedisConnectionManager.Set(ipPort, IsMaster, cnn);
                }
                else
                {
                    throw new Exception($"当前节点不可达ipport:{ipPort}");
                }
            }
            else
            {
                _cnn = cnn;
            }

            switch (operationType)
            {
                case OperationType.Do:
                    return _cnn.Do((RequestType)args[0]);
                case OperationType.DoBatchWithList:
                    return _cnn.DoMultiLineWithList((RequestType)args[0], (string)args[1], (List<string>)args[2]);
                case OperationType.DoBatchWithDic:
                    return _cnn.DoMultiLineWithDic((RequestType)args[0], (Dictionary<string, string>)args[1]);
                case OperationType.DoBatchWithIDDic:
                    return _cnn.DoBatchWithIDDic((RequestType)args[0], (string)args[1], (Dictionary<string, string>)args[2]);
                case OperationType.DoBatchZaddWithIDDic:
                    return _cnn.DoBatchZaddWithIDDic((RequestType)args[0], (string)args[1], (Dictionary<double, string>)args[2]);
                case OperationType.DoBatchWithIDKeys:
                    return _cnn.DoBatchWithIDKeys((RequestType)args[0], (string)args[1], (string[])args[3]);
                case OperationType.DoBatchWithParams:
                    return _cnn.DoWithMutiParams((RequestType)args[0], (string[])args[1]);
                case OperationType.DoCluster:
                    return _cnn.DoMutiCmd((RequestType)args[0], (object[])args[1]);
                case OperationType.DoClusterSetSlot:
                    return _cnn.DoClusterSetSlot((RequestType)args[0], (string)args[1], (int)args[2], (string)args[3]);
                case OperationType.DoExpire:
                    return _cnn.DoExpire((string)args[0], (int)args[1]);
                case OperationType.DoExpireAt:
                    return _cnn.DoExpireAt((string)args[0], (int)args[1]);
                case OperationType.DoExpireInsert:
                    return _cnn.DoExpireInsert((RequestType)args[0], (string)args[1], (string)args[2], (int)args[3]);
                case OperationType.DoWithID:
                    return _cnn.DoWithID((RequestType)args[0], (string)args[1], (string)args[2], (string)args[3]);
                case OperationType.DoRang:
                    return _cnn.DoRang((RequestType)args[0], (string)args[1], (double)args[2], (double)args[3]);
                case OperationType.DoRangByScore:
                    return _cnn.DoRangByScore((RequestType)args[0], (string)args[1], (double)args[2], (double)args[3], (RangType)args[4], (long)args[5], (int)args[6], (bool)args[7]);
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
        private void GetClusterMap()
        {
            var clusterNodes = ClusterNodes;

            if (clusterNodes != null && clusterNodes.Any())
            {
                foreach (var item in clusterNodes)
                {
                    if (!RedisConnectionManager.Exsits(item.IPPort))
                    {
                        var cnn = new RedisConnection(item.IPPort);
                        cnn.OnRedirect += _redisConnection_OnRedirect;
                        cnn.OnDisconnected += _cnn_OnDisconnected;
                        cnn.Connect();
                        if (!string.IsNullOrEmpty(RedisConfig.Passwords))
                            cnn.Auth(RedisConfig.Passwords);
                        var isMaster = item.IsMaster;
                        cnn.RedisServerType = isMaster ? RedisServerType.ClusterMaster : RedisServerType.ClusterSlave;
                        RedisConnectionManager.Set(item.IPPort, isMaster, cnn);
                    }
                }
            }
        }


        public bool _hasJudged = false;

        public bool _isCluster = false;

        /// <summary>
        /// 是否是cluster node
        /// </summary>
        public bool IsCluster
        {
            get
            {
                if (!_hasJudged)
                {
                    _hasJudged = true;
                    var info = Info("Cluster");
                    if (info.Contains("cluster_enabled:1"))
                    {
                        _isCluster = true;
                    }
                }
                return _isCluster;
            }
        }
        /// <summary>
        /// 集群信息
        /// </summary>
        /// <returns></returns>
        public string ClusterInfoStr()
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_INFO).Data;
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
            return _cnn.DoMutiCmd(RequestType.CLUSTER_NODES).Data;
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
            return _cnn.DoMutiCmd(RequestType.CLUSTER_MEET, ip, port).Data == OK;
        }
        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="nodeID"></param>
        /// <returns></returns>
        public bool DeleteNode(string nodeID)
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_FORGET, nodeID).Data == OK;
        }

        /// <summary>
        /// 将当前节点改成指定节点的从节点
        /// </summary>
        /// <param name="masterNodeID"></param>
        /// <returns></returns>
        public bool Replicate(string masterNodeID)
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_REPLICATE, masterNodeID).Data == OK;
        }

        /// <summary>
        /// 该命令只能在群集的某个slave节点执行，让slave节点进行一次人工故障切换。
        /// FORCE|TAKEOVER,
        /// FORCE 选项:master节点down的情况下的人工故障转移。slave节点不和master协商(master也许已不可达)，从上如4步开始进行故障切换。当master已不可用，而我们想要做人工故障转移时，该选项很有用。
        /// TAKEOVER 选项: 忽略群集一致验证的的人工故障切换。选项TAKEOVER 实现了FORCE的所有功能，同时为了能够进行故障切换放弃群集验证
        /// </summary>
        /// <param name="force"></param>
        /// <returns></returns>
        public bool BeMaster(bool force)
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_FAILOVER, (force ? "FORCE" : "TAKEOVER")).Data == OK;
        }

        /// <summary>
        /// 当前节点添加slots
        /// </summary>
        /// <param name="slots"></param>
        /// <returns></returns>
        public bool AddSlots(params int[] slots)
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_ADDSLOTS, slots).Data == OK;
        }

        /// <summary>
        /// 当前节点移除slots
        /// </summary>
        /// <param name="slots"></param>
        /// <returns></returns>
        public bool DelSlots(params int[] slots)
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_DELSLOTS, slots).Data == OK;
        }
        /// <summary>
        /// 当前节点移除全部slots
        /// </summary>
        /// <returns></returns>
        public bool FlushSlots()
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_FLUSHSLOTS).Data == OK;
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
            int.TryParse(_cnn.DoMutiCmd(RequestType.CLUSTER_KEYSLOT, key).Data, out slot);
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
            int.TryParse(_cnn.DoMutiCmd(RequestType.CLUSTER_COUNTKEYSINSLOT, slot).Data, out count);
            return count;
        }

        /// <summary>
        /// 返回一个slot中count个key的集合
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public List<string> GetKeysInSlot(int slot)
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_GETKEYSINSLOT, slot).ToList();
        }

        /// <summary>
        /// 将节点的配置保存到配置文件
        /// </summary>
        /// <returns></returns>
        public bool SaveClusterConfig()
        {
            return _cnn.DoMutiCmd(RequestType.CLUSTER_SAVECONFIG).Data == OK;
        }
    }
}
