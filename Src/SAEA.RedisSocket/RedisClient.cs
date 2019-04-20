/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.RedisSocket
*文件名： RedisClient
*版本号： v4.3.3.7
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
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SAEA.RedisSocket
{
    public partial class RedisClient
    {
        RedisConnection _cnn;

        RedisDataBase _redisDataBase;

        const string OK = RedisConst.OK;

        object _syncLocker = new object();

        int _dbIndex = 0;

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

        public RedisClient(string ipPort, string password, int acitonTimeout = 60, bool debugModel = false) : this(new RedisConfig(ipPort, password, acitonTimeout), debugModel)
        {

        }

        /// <summary>
        /// 连接断开事件
        /// </summary>
        /// <param name="obj"></param>
        private void _cnn_OnDisconnected(string ipPort)
        {
            RedisConnectionManager.Remove(ipPort);
            RedisConnectionManager.RemoveClusterNode(ipPort);
        }

        /// <summary>
        /// 使用密码连接到RedisServer
        /// </summary>
        /// <returns></returns>
        public string Connect()
        {
            lock (_syncLocker)
            {
                if (!IsConnected)
                {
                    _cnn.Connect();

                    IsConnected = _cnn.IsConnected;

                    var infoMsg = Info();

                    if (infoMsg.Contains(RedisConst.NOAuth))
                    {
                        if (string.IsNullOrEmpty(RedisConfig.Passwords))
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
                    GetClusterMap(ipPort);
                }
                else
                {
                    _cnn.RedisServerType = isMaster ? RedisServerType.Master : RedisServerType.Slave;
                    RedisConnectionManager.Set(ipPort, _cnn);
                }
                return OK;
            }
        }


        /// <summary>
        /// 保持redis连接
        /// </summary>
        private void KeepAlive()
        {
            ThreadHelper.Run(() =>
            {
                while (_cnn.IsConnected)
                {
                    if (_cnn.Actived.AddSeconds(60) <= DateTimeHelper.Now)
                    {
                        Ping();
                    }
                    ThreadHelper.Sleep(1000);
                }
            }, true, ThreadPriority.Highest);
        }


        /// <summary>
        /// console命令行
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public string Console(string cmd)
        {
            return _cnn.RequestWithConsole(cmd).ToString();
        }

        /// <summary>
        /// redis 密码验证
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public string Auth(string password)
        {
            return _cnn.DoInOne(RequestType.AUTH, password).Data;
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
                _dbIndex = dbIndex;
            }
            if (_cnn.DoInOne(RequestType.SELECT, _dbIndex.ToString()).Data.IndexOf(RedisConst.ErrIndex) == -1)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 当前db数据项数据
        /// </summary>
        /// <returns></returns>
        public int DBSize()
        {
            var result = 0;
            int.TryParse(_cnn.Do(RequestType.DBSIZE).Data, out result);
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
            return _cnn.DoInOne(RequestType.INFO, section).Data;
        }

        /// <summary>
        /// redis信息
        /// </summary>
        public ServerInfo ServerInfo
        {
            get
            {
                ServerInfo serverInfo = new ServerInfo() { address = RedisConfig.GetIPPort() };

                var info = Info();

                var lines = info.Split(RedisConst.Enter, StringSplitOptions.RemoveEmptyEntries);

                if (lines != null && lines.Any())
                {
                    Dictionary<string, string> dic = new Dictionary<string, string>();

                    foreach (var item in lines)
                    {
                        if (item.IndexOf("#") > -1)
                            continue;

                        var arr = item.Split(":");

                        if (arr.Length > 1)
                            dic[arr[0]] = arr[1];
                    }

                    if (dic.TryGetValue("config_file", out string config_file))
                    {
                        serverInfo.config_file = config_file;
                    }
                    if (dic.TryGetValue("connected_clients", out string connected_clients))
                    {
                        serverInfo.connected_clients = connected_clients;
                    }
                    if (dic.TryGetValue("connected_slaves", out string connected_slaves))
                    {
                        serverInfo.connected_slaves = connected_slaves;
                    }
                    if (dic.TryGetValue("os", out string os))
                    {
                        serverInfo.os = os;
                    }
                    if (dic.TryGetValue("redis_version", out string redis_version))
                    {
                        serverInfo.redis_version = redis_version;
                    }
                    if (dic.TryGetValue("role", out string role))
                    {
                        serverInfo.role = role;
                    }
                    if (dic.TryGetValue("used_cpu_sys", out string used_cpu_sys))
                    {
                        serverInfo.used_cpu_sys = used_cpu_sys;
                    }
                    if (dic.TryGetValue("used_cpu_user", out string used_cpu_user))
                    {
                        serverInfo.used_cpu_user = used_cpu_user;
                    }
                    if (dic.TryGetValue("used_memory", out string used_memory))
                    {
                        serverInfo.used_memory = used_memory;
                    }
                    if (dic.TryGetValue("used_memory_human", out string used_memory_human))
                    {
                        serverInfo.used_memory_human = used_memory_human;
                    }
                    if (dic.TryGetValue("used_memory_peak_human", out string used_memory_peak_human))
                    {
                        serverInfo.used_memory_peak_human = used_memory_peak_human;
                    }


                    if (dic.ContainsKey("cluster_enabled"))
                    {
                        serverInfo.cluster_enabled = dic["cluster_enabled"];
                    }
                    if (dic.ContainsKey("executable"))
                    {
                        serverInfo.executable = dic["executable"];
                    }
                    if (dic.ContainsKey("maxmemory_human"))
                    {
                        serverInfo.maxmemory_human = dic["maxmemory_human"];
                    }
                    if (dic.ContainsKey("used_memory_rss_human"))
                    {
                        serverInfo.used_memory_rss_human = dic["used_memory_rss_human"];
                    }
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
            return _cnn.DoInOne(RequestType.SLAVEOF, string.IsNullOrEmpty(ipPort) ? RedisConst.NoOne : ipPort).Data;
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
        public int DBIndex
        {
            get
            {
                return _dbIndex;
            }
        }

        /// <summary>
        /// 获取redis database操作
        /// </summary>
        /// <param name="dbIndex"></param>
        /// <returns></returns>
        public RedisDataBase GetDataBase(int dbIndex = -1)
        {
            lock (_syncLocker)
            {
                if (dbIndex >= 0 && dbIndex != _dbIndex)
                {
                    _dbIndex = dbIndex;
                    Select(_dbIndex);
                }
                if (_redisDataBase == null)
                {
                    _redisDataBase = new RedisDataBase(_cnn);
                }
                return _redisDataBase;
            }
        }
    }
}
