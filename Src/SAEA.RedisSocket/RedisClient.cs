﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.RedisSocket
*文件名： RedisClient
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Core.Stream;
using SAEA.RedisSocket.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.RedisSocket
{
    public partial class RedisClient
    {
        RedisConnection _cnn;

        RedisDataBase _redisDataBase;

        const string OK = RedisConst.OK;

        object _syncLocker = new object();

        bool _debugModel = false;

        public bool IsConnected { get; set; }

        public RedisConfig RedisConfig
        {
            get;
            set;
        }

        public RedisClient(RedisConfig config, bool debugModel = false)
        {
            _debugModel = debugModel;
            RedisConfig = config;
            if (_debugModel)
            {
                _cnn = new RedisConnectionDebug(RedisConfig.GetIPPort(), RedisConfig.ActionTimeOut);
            }
            else
            {
                _cnn = new RedisConnection(RedisConfig.GetIPPort(), RedisConfig.ActionTimeOut);
            }
            _cnn.OnRedirect += _redisConnection_OnRedirect;
            _cnn.OnDisconnected += _cnn_OnDisconnected;
        }

        public RedisClient(string connectStr, bool debugModel = false) : this(new RedisConfig(connectStr), debugModel) { }

        public RedisClient(string ipPort, string password, int acitonTimeout = 6 * 1000, bool debugModel = false) : this(new RedisConfig(ipPort, password, acitonTimeout), debugModel)
        {

        }

        /// <summary>
        /// 连接断开事件
        /// </summary>
        /// <param name="obj"></param>
        private void _cnn_OnDisconnected(string ipPort)
        {
            LogHelper.Error("RedisClient disconnected", new RedisIOException(), ipPort);
        }

        /// <summary>
        /// 使用密码连接到RedisServer
        /// </summary>
        /// <returns></returns>
        public string Connect()
        {
            if (_cnn.Connect())
            {
                IsConnected = _cnn.IsConnected;

                var infoMsg = Info();

                if (infoMsg.Contains(RedisConst.NOAuth))
                {
                    if (string.IsNullOrWhiteSpace(RedisConfig.Passwords))
                    {
                        _cnn.Quit();
                        return infoMsg;
                    }

                    var authMsg = Auth(RedisConfig.Passwords);

                    if (string.Compare(authMsg, OK, true) != 0)
                    {
                        _cnn.Quit();
                        return authMsg;
                    }
                }
            }

            _cnn.KeepAlived(() => this.KeepAlive());

            var ipPort = RedisConfig.GetIPPort();

            var isMaster = this.IsMaster;
            var isCluster = this.IsCluster;

            if (isCluster)
            {
                _cnn.RedisServerType = isMaster ? RedisServerType.ClusterMaster : RedisServerType.ClusterSlave;
                GetClusterMap();
            }
            else
            {
                _cnn.RedisServerType = isMaster ? RedisServerType.Master : RedisServerType.Slave;
                RedisConnectionManager.Set(ipPort, isMaster, _cnn);
            }
            return OK;
        }


        /// <summary>
        /// 保持redis连接
        /// </summary>
        private void KeepAlive()
        {
            Task.Factory.StartNew(() =>
            {
                while (_cnn.IsConnected)
                {
                    if (_cnn.Actived.AddSeconds(30) <= DateTimeHelper.Now)
                    {
                        Ping();
                    }
                    ThreadHelper.Sleep(1000);
                }
            }, TaskCreationOptions.LongRunning);
        }


        /// <summary>
        /// console命令行
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public ResponseData<string> Console(string cmd)
        {
            var result = _cnn.RequestWithConsole(cmd);

            if (result != null && !string.IsNullOrEmpty(result.Data))
            {
                result.Data = result.Data.Replace(RedisCoder.SEPARATOR, "\r\n");
            }

            return result;
        }

        /// <summary>
        /// redis 密码验证
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public string Auth(string password)
        {
            return _cnn.Auth(password);
        }

        /// <summary>
        /// redis ping
        /// </summary>
        /// <returns></returns>
        public string Ping()
        {
            return _cnn.Do(RequestType.PING).Data;
        }

        /// <summary>
        /// 选择redis database
        /// </summary>
        /// <param name="dbIndex"></param>
        /// <returns></returns>
        public bool Select(int dbIndex = -1)
        {
            if (dbIndex > -1)
            {
                DBIndex = dbIndex;
            }
            if (!IsCluster)
            {
                if (_cnn.DoWithOne(RequestType.SELECT, DBIndex.ToString()).Data.IndexOf(RedisConst.ErrIndex, StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 当前db数据项数据
        /// </summary>
        /// <returns></returns>
        public long DBSize()
        {
            long result;
            long.TryParse(_cnn.Do(RequestType.DBSIZE).Data, out result);
            return result;
        }
        /// <summary>
        /// 获取某个集合的类型 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Type(string key)
        {
            return _cnn.DoWithKey(RequestType.TYPE, key).Data;
        }
        /// <summary>
        /// redis server的信息
        /// </summary>
        /// <returns></returns>
        public string Info(string section = RedisConst.All)
        {
            return _cnn.DoWithOne(RequestType.INFO, section).Data;
        }


        /// <summary>
        /// redis server信息
        /// </summary>
        public RedisServerInfo ServerInfo
        {
            get
            {
                RedisServerInfo serverInfo = new RedisServerInfo() { Address = RedisConfig.GetIPPort() };

                try
                {
                    var info = Info();

                    var lines = info.Split(RedisConst.Enter, StringSplitOptions.RemoveEmptyEntries);

                    if (lines != null && lines.Any())
                    {
                        foreach (var line in lines)
                        {
                            var sarr = line.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);

                            switch (sarr[0])
                            {
                                case "used_cpu_user":
                                    serverInfo.Cpu = double.Parse(sarr[1]);
                                    break;
                                case "used_memory":
                                    serverInfo.Memory = double.Parse(sarr[1]) / 1024 / 1024;
                                    break;
                                case "instantaneous_ops_per_sec":
                                    serverInfo.Cmds = long.Parse(sarr[1]);
                                    break;
                                case "instantaneous_input_kbps":
                                    serverInfo.Input = double.Parse(sarr[1]);
                                    break;
                                case "instantaneous_output_kbps":
                                    serverInfo.Output = double.Parse(sarr[1]);
                                    break;
                                case "connected_clients":
                                    serverInfo.Clients = int.Parse(sarr[1]);
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error("RedisClient.ServerInfo", ex, RedisConfig);
                }
                return serverInfo;
            }
        }
        /// <summary>
        /// 设置或取消丛
        /// </summary>
        /// <param name="ipPort">格式：ip port，若为空则为取消</param>
        /// <returns></returns>
        public string SlaveOf(string ipPort = RedisConst.Empty)
        {
            return _cnn.DoWithOne(RequestType.SLAVEOF, string.IsNullOrEmpty(ipPort) ? RedisConst.NoOne : ipPort).Data;
        }
        /// <summary>
        /// redis server是否是主
        /// </summary>
        /// <returns></returns>
        public bool IsMaster
        {
            get
            {
                var info = Info("Replication");
                if (info.Contains("role:master"))
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 当前db index
        /// </summary>
        public int DBIndex { get; private set; } = 0;

        /// <summary>
        /// 获取redis database操作
        /// </summary>
        /// <param name="dbIndex"></param>
        /// <returns></returns>
        public RedisDataBase GetDataBase(int dbIndex = -1)
        {
            if (dbIndex >= 0 && dbIndex != DBIndex)
            {
                lock (_syncLocker)
                {
                    if (dbIndex >= 0 && dbIndex != DBIndex)
                    {
                        DBIndex = dbIndex;
                        Select(DBIndex);
                    }
                }
            }
            if (_redisDataBase == null)
            {
                lock (_syncLocker)
                {
                    if (_redisDataBase == null)
                        _redisDataBase = new RedisDataBase(_cnn);
                }
            }
            return _redisDataBase;
        }

        #region redis stream

        RedisProducer _redisProducer = null;

        /// <summary>
        /// RedisQueue
        /// </summary>
        /// <returns></returns>
        public RedisQueue GetRedisQueue()
        {
            return new RedisQueue(_cnn);
        }

        /// <summary>
        /// GetRedisProducer
        /// </summary>
        /// <returns></returns>
        public RedisProducer GetRedisProducer()
        {
            if (_redisProducer == null)
            {
                _redisProducer = new RedisProducer(_cnn);
            }
            return _redisProducer;

        }
        /// <summary>
        /// GetRedisConsumer
        /// </summary>
        /// <param name="topicIDs"></param>
        /// <param name="count"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public RedisConsumer GetRedisConsumer(IEnumerable<TopicID> topicIDs, int count = 1, bool blocked = false, int timeout = 1000)
        {
            var cnn = new RedisConnection(RedisConfig.GetIPPort(), RedisConfig.ActionTimeOut);
            cnn.Connect();
            if (!string.IsNullOrEmpty(RedisConfig.Passwords))
            {
                cnn.Auth(RedisConfig.Passwords);
            }
            return new RedisConsumer(cnn, topicIDs, count, blocked, timeout);
        }
        /// <summary>
        /// GetRedisGroupConsumer
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="consumerName"></param>
        /// <param name="topicName"></param>
        /// <param name="count"></param>
        /// <param name="autoCommit"></param>
        /// <param name="redisId"></param>
        /// <param name="noAck"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        /// <param name="asc"></param>
        /// <returns></returns>
        public RedisGroupConsumer GetRedisGroupConsumer(string groupName, string consumerName, string topicName, int count = 1, bool autoCommit = false, string redisId = "", bool noAck = false, bool blocked = false, int timeout = 1000, bool asc = true)
        {
            var cnn = new RedisConnection(RedisConfig.GetIPPort(), RedisConfig.ActionTimeOut);
            cnn.Connect();
            if (!string.IsNullOrEmpty(RedisConfig.Passwords))
            {
                cnn.Auth(RedisConfig.Passwords);
            }
            return new RedisGroupConsumer(cnn, groupName, consumerName, topicName, count, autoCommit, redisId, noAck, blocked, timeout, asc);
        }

        #endregion

        /// <summary>
        /// 获取服务器配置
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public string GetConfig(string parameter)
        {
            var responseData = _cnn.DoMutiCmd(RequestType.CONFIG_GET, parameter);

            if (responseData.Type == ResponseType.Lines)
            {
                return responseData.ToList()[1];
            }

            return string.Empty;
        }

        /// <summary>
        /// 设置服务器配置
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetConfig(string parameter, object value)
        {
            return _cnn.DoMutiCmd(RequestType.CONFIG_SET, parameter, value.ToString()).Type == ResponseType.OK ? true : false;
        }

        /// <summary>
        /// 获取客户端列表
        /// </summary>
        /// <returns></returns>
        public List<string> ClientList()
        {
            var list = _cnn.DoMutiCmd(RequestType.CLIENT_LIST).ToList();

            if (list != null && list.Any())
            {
                var result = new List<string>();

                var arr = list[0].Split("id=", StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in arr)
                {
                    result.Add($"id={item}");
                }

                return result;
            }

            return null;
        }

        /// <summary>
        /// FlushAll,清空整个 Redis 服务器的数据
        /// </summary>
        /// <returns></returns>
        public bool FlushAll()
        {
            var data = _cnn.Do(RequestType.FLUSHALL);

            if (data != null)
            {
                if (int.TryParse(data.Data, out int res) && res > -1)

                    return true;
            }

            return false;
        }
    }
}
