/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RedisConfig
*版本号： V1.0.0.0
*唯一标识：db83d010-c090-4572-94ec-82cc543cf062
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/19 10:40:07
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/19 10:40:07
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Model
{
    public class PooledRedisClient : IClient, IDisposable
    {

        public bool IsConnected
        {
            get; set;
        } = false;

        RedisClientFactory _factory;

        RedisClient _client;

        internal void SetClient(RedisClientFactory factory, IClient client)
        {
            _factory = factory;
            _client = client as RedisClient;
            IsConnected = client.IsConnected;
        }

        public string Auth(string password)
        {
            return _client.Auth(password);
        }

        public string Ping()
        {
            return _client.Ping();
        }

        public bool Select(int dbIndex = 0)
        {
            return _client.Select(dbIndex);
        }

        public int DBSize()
        {
            return _client.DBSize();
        }

        public string Type(string key)
        {
            return _client.Type(key);
        }

        public string Info(string section = "all")
        {
            return _client.Info(section);
        }

        public string SlaveOf(string ipPort = "")
        {
            return _client.SlaveOf(ipPort);
        }

        public bool IsMaster
        {
            get
            {
                return _client.IsMaster;
            }
        }
        public bool IsCluster
        {
            get
            {
                return _client.IsCluster;
            }
        }

        public int DBIndex
        {
            get
            {
                return _client.DBIndex;
            }
        }

        public RedisDataBase GetDataBase(int dbIndex = -1)
        {
            return _client.GetDataBase(dbIndex);
        }


        public void Dispose()
        {
            bool isMaster = IsMaster;
            _factory.Free(isMaster, _client);
            IsConnected = false;
            _client = null;
            _factory = null;
        }
    }
}
