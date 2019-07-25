using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 服务器配置信息
    /// </summary>
    public class ServerInfo
    {
        public string redis_version
        {
            get; set;
        }
        /// <summary>
        /// ip:port
        /// </summary>
        public string address
        {
            get; set;
        }

        public string os
        {
            get; set;
        }

        public string used_cpu_user
        {
            get; set;
        }
        public string used_memory
        {
            get; set;
        }
        public string used_cpu_sys
        {
            get; set;
        }

        public string connected_clients
        {
            get; set;
        }

        public string used_memory_human
        {
            get; set;
        }

        public string used_memory_rss_human
        {
            get; set;
        }

        public string used_memory_peak_human
        {
            get; set;
        }

        public string maxmemory
        {
            get; set;
        }
        public string maxmemory_human
        {
            get; set;
        }

        public string role
        {
            get; set;
        }

        public string connected_slaves
        {
            get; set;
        }

        public string cluster_enabled
        {
            get; set;
        }

        public string executable
        {
            get; set;
        }

        public string config_file
        {
            get; set;
        }

    }
}
