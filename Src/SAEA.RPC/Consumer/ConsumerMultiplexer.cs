/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Consumer
*文件名： ConsumerMultiplexer
*版本号： v4.3.1.2
*唯一标识：85b40df2-6436-4a63-8358-6a0ed32b20cd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/25 16:14:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/25 16:14:32
*修改人： yswenli
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// 使用多路复用概念来实现高效率传输
    /// </summary>
    public class ConsumerMultiplexer : ISyncBase, IDisposable
    {
        static HashMap<string, int, RClient> _hashMap = new HashMap<string, int, RClient>();

        int _index = 0;

        Uri _uri = null;

        int _links = 4;

        int _timeOut = 10 * 1000;

        int _retry = 5;

        ConcurrentDictionary<int, RClient> _myClients = new ConcurrentDictionary<int, RClient>();


        object _syncLocker = new object();
        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        /// <param name="timeOut"></param>
        ConsumerMultiplexer(Uri uri, int links = 10, int timeOut = 10 * 1000)
        {
            _uri = uri;
            _links = links;
            _timeOut = timeOut;

            var dic = _hashMap.GetAll(uri.ToString());

            for (int i = 0; i < _links; i++)
            {
                var rClient = dic[i];
                rClient.OnDisconnected -= RClient_OnDisconnected;
                rClient.OnError -= RClient_OnError;
                rClient.OnDisconnected += RClient_OnDisconnected;
                rClient.OnError += RClient_OnError;
                _myClients.TryAdd(i, rClient);
            }
        }

        /// <summary>
        /// 创建多路复用
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static ConsumerMultiplexer Create(Uri uri, int links = 4, int timeOut = 10 * 1000)
        {
            if (!_hashMap.Exits(uri.ToString()))
            {
                for (int i = 0; i < links; i++)
                {
                    var rClient = new RClient(uri);
                    rClient.Connect();                    
                    rClient.KeepAlive();
                    _hashMap.Set(uri.ToString(), i, rClient);
                }
            }
            return new ConsumerMultiplexer(uri, links, timeOut);
        }


        #region events

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        private void RClient_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void RClient_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }


        #endregion

        /// <summary>
        /// 使用多路连接发送数据
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public byte[] Request(string serviceName, string method, byte[] args, int retry = 5)
        {
            _retry = retry;
            return ReTryHelper.Do(() => this.GetClient().Request(serviceName, method, args, _timeOut), retry);
        }

        /// <summary>
        /// 获取缓存的连接
        /// </summary>
        /// <returns></returns>
        private RClient GetClient()
        {
            lock (_syncLocker)
            {
                RClient rClient;
                do
                {
                    if (_index >= _links)
                    {
                        ExceptionCollector.Add("Consumer", new RPCSocketException("连接已断开！"));
                        return null;
                    }

                    if (_myClients.TryGetValue(_index, out rClient) && rClient.Connected)
                    {
                        break;
                    }

                    var retry = 0;

                    while (!rClient.Connected && retry < _retry)
                    {
                        rClient.Connect();

                        ThreadHelper.Sleep(3 * 1000);

                        retry++;

                        ConsoleHelper.WriteLine($"ConsumerMultiplexer正在修复连接，当前是第{retry}次！");
                    }

                    _index++;
                }
                while (!rClient.Connected);

                _index = 0;

                return rClient;
            }
        }

        public void Dispose()
        {
            foreach (var rClient in _myClients.Values)
            {
                rClient.Dispose();
            }
            _hashMap.Remove(_uri.ToString());
            _myClients.Clear();
        }
    }
}
