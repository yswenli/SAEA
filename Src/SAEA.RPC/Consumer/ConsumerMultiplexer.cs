/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Consumer
*文件名： ConsumerMultiplexer
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// Consumer多路复用
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

        public bool IsConnected
        {
            get; private set;
        } = false;


        /// <summary>
        /// Consumer多路复用
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        /// <param name="timeOut"></param>
        /// <param name="retry"></param>
        ConsumerMultiplexer(Uri uri, int links = 10, int timeOut = 10 * 1000, int retry = 5)
        {
            _uri = uri;
            _links = links;
            _timeOut = timeOut;
            _retry = retry;

            if (!_hashMap.Exits(uri.ToString()))
            {
                for (int i = 0; i < links; i++)
                {
                    var rClient = new RClient(uri, timeOut);

                    rClient.OnDisconnected -= RClient_OnDisconnected;
                    rClient.OnError -= RClient_OnError;
                    rClient.OnNoticed -= RClient_OnNoticed;
                    rClient.OnDisconnected += RClient_OnDisconnected;
                    rClient.OnError += RClient_OnError;
                    rClient.OnNoticed += RClient_OnNoticed;

                    rClient.Connect();
                    rClient.KeepAlive();

                    _hashMap.Set(uri.ToString(), i, rClient);

                    _myClients.TryAdd(i, rClient);
                }
            }
        }

        /// <summary>
        /// 创建多路复用
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        /// <param name="timeOut"></param>
        /// <param name="retry"></param>
        /// <returns></returns>
        public static ConsumerMultiplexer Create(Uri uri, int links = 4, int timeOut = 3 * 1000, int retry = 5)
        {
            var cm = new ConsumerMultiplexer(uri, links, timeOut, retry);
            cm.IsConnected = true;
            return cm;
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public bool Reconnect()
        {
            var dic = _hashMap.GetAll(_uri.ToString());

            if (dic != null && dic.Any())
            {
                foreach (var item in dic)
                {
                    item.Value.Connect();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ReconnectAsync()
        {
            await Task.Yield();

            return Reconnect();
        }

        private void RClient_OnNoticed(byte[] serializeData)
        {
            OnNoticed?.Invoke(serializeData);
        }


        #region events

        public event OnNoticedHandler OnNoticed;

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
        public byte[] Request(string serviceName, string method, byte[] args)
        {
            return ReTryHelper.Do(() => this.GetClient().Request(serviceName, method, args), _retry);
        }

        /// <summary>
        /// 注册通知
        /// </summary>
        public void RegistReceiveNotice()
        {
            this.GetClient().RegistReceiveNotice();
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
                        this.IsConnected = false;
                        ExceptionCollector.Add("Consumer.GetClient.Error", new RPCSocketException("连接已断开！"));
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
