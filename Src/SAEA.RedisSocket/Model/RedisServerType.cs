using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis server type
    /// </summary>
    public enum RedisServerType
    {
        Master = 0,
        Slave = 1,
        ClusterMaster = 2,
        ClusterSlave = 3
    }
}
