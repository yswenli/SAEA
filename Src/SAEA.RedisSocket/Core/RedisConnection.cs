/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnection
*版本号： v5.0.0.1
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.RedisSocket.Base;
using SAEA.RedisSocket.Base.Net;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// 连接包装类
    /// </summary>
    internal partial class RedisConnection
    {
        object _syncLocker = new object();

        RClient _cnn;

        DateTime _actived;

        public DateTime Actived
        {
            get
            {
                return _actived;
            }
        }

        RedisCoder _redisCoder;

        internal RedisCoder RedisCoder
        {
            get
            {
                return _redisCoder;
            }
        }

        public bool IsConnected { get; private set; } = false;


        public RedisServerType RedisServerType { get; set; }


        public string IPPort { get; set; }


        public event Action<string> OnDisconnected;


        /// <summary>
        /// 连接转向事件
        /// </summary>
        public event RedirectHandler OnRedirect;


        int _actionTimeout = 10;

        public RedisConnection(string ipPort, int actionTimeout = 10)
        {
            this.IPPort = ipPort;
            var address = ipPort.ToIPPort();
            _actionTimeout = actionTimeout;
            _cnn = new RClient(102400, address.Item1, address.Item2);
            _cnn.OnActived += _cnn_OnActived;
            _cnn.OnMessage += _cnn_OnMessage;
            _cnn.OnDisconnected += _cnn_OnDisconnected;
            _redisCoder = new RedisCoder(_cnn, actionTimeout);
        }

        private void _cnn_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(this.IPPort);
            ThreadHelper.Sleep(3000);
            IsConnected = false;
            Connect();
        }

        private void _cnn_OnActived(DateTime actived)
        {
            _actived = actived;
        }


        protected virtual void _cnn_OnMessage(byte[] msg)
        {
            RedisCoder.Enqueue(msg);
        }

        /// <summary>
        /// 连接到redisServer
        /// </summary>
        public bool Connect(TimeSpan timeSpan)
        {
            return ConnectAsync(timeSpan).Result;
        }

        /// <summary>
        /// 连接到redisServer
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            return Connect(TimeSpan.FromSeconds(_actionTimeout));
        }

        /// <summary>
        /// 发送,
        /// 命令行模式
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        internal ResponseData RequestWithConsole(string cmd)
        {
            return RequestWithConsoleAsync(cmd, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="cmd"></param>
        public ResponseData Do(RequestType type)
        {
            return DoAsync(type, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public string Auth(string password)
        {
            return DoWithOne(RequestType.AUTH, password).Data;
        }

        /// <summary>
        /// 用于不会迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public ResponseData DoWithOne(RequestType type, string content)
        {
            return DoWithOneAsync(type, content, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        /// <summary>
        /// 用于可以迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ResponseData DoWithKey(RequestType type, string key)
        {
            return DoWithKeyAsync(type, key, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public ResponseData DoWithKeyValue(RequestType type, string key, string value)
        {
            return DoWithKeyValueAsync(type, key, value, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public ResponseData DoWithID(RequestType type, string id, string key, string value)
        {
            return DoWithIDAsync(type, id, key, value, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public ResponseData DoWithMutiParams(RequestType type, params string[] keys)
        {
            return DoWithMutiParamsAsync(type, TimeSpan.FromSeconds(_actionTimeout), keys).Result;
        }

        public void DoExpire(string key, int seconds)
        {
            DoExpireAsync(key, seconds).Wait(TimeSpan.FromSeconds(_actionTimeout));
        }

        public void DoExpireAt(string key, int timestamp)
        {
            DoExpireAtAsync(key, timestamp).Wait(TimeSpan.FromSeconds(_actionTimeout));
        }

        public void DoExpireInsert(RequestType type, string key, string value, int seconds)
        {
            DoExpireInsertAsync(type, key, value, seconds).Wait(TimeSpan.FromSeconds(_actionTimeout));
        }

        public ResponseData DoRang(RequestType type, string key, double begin = 0, double end = -1)
        {
            return DoRangAsync(type, key, begin, end, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public ResponseData DoRangByScore(RequestType type, string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20, bool withScore = false)
        {
            return DoRangByScoreAsync(TimeSpan.FromSeconds(_actionTimeout), type, key, min, max, rangType, offset, count, withScore).Result;
        }

        public void DoSub(string[] channels, Action<string, string> onMsg)
        {
            lock (_syncLocker)
            {
                RedisCoder.Request(RequestType.SUBSCRIBE, channels);
                RedisCoder.IsSubed = true;

                TaskHelper.Run(() =>
                {
                    while (RedisCoder.IsSubed)
                    {
                        var result = RedisCoder.Decoder(RequestType.SUBSCRIBE);
                        if (result.Type == ResponseType.Sub)
                        {
                            var arr = result.Data.ToArray(false, Environment.NewLine);
                            onMsg.Invoke(arr[0], arr[1]);
                        }
                        if (result.Type == ResponseType.UnSub)
                        {
                            break;
                        }
                    }
                });
            }
        }

        public ResponseData DoBatchWithList(RequestType type, string id, List<string> list)
        {
            return DoBatchWithListAsync(type, id, list, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public ResponseData DoBatchWithDic(RequestType type, Dictionary<string, string> dic)
        {
            return DoBatchWithDicAsync(type, dic, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }


        public ResponseData DoBatchWithIDKeys(RequestType type, string id, params string[] keys)
        {
            return DoBatchWithIDKeysAsync(TimeSpan.FromSeconds(_actionTimeout), type, id, keys).Result;
        }

        public ResponseData DoBatchZaddWithIDDic(RequestType type, string id, Dictionary<double, string> dic)
        {
            return DoBatchZaddWithIDDicAsync(type, id, dic, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        public ResponseData DoBatchWithIDDic(RequestType type, string id, Dictionary<string, string> dic)
        {
            return DoBatchWithIDDicAsync(type, id, dic, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        /// <summary>
        /// SCAN
        /// </summary>
        /// <param name="type"></param>
        /// <param name="offset"></param>
        /// <param name="pattern"></param>
        /// <param name="count"></param>
        /// <returns></returns>

        public ScanResponse DoScan(RequestType type, int offset = 0, string pattern = "*", int count = -1)
        {
            return DoScanAsync(TimeSpan.FromSeconds(_actionTimeout), type, offset, pattern, count).Result;
        }

        /// <summary>
        /// Others Scan
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="offset"></param>
        /// <param name="pattern"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public ScanResponse DoScanKey(RequestType type, string key, int offset = 0, string pattern = "*", int count = -1)
        {
            return DoScanKeyAsync(TimeSpan.FromSeconds(_actionTimeout), type, key, offset, pattern, count).Result;
        }


        public ResponseData DoMutiCmd(RequestType type, params object[] @params)
        {
            return DoMutiCmdAsync(TimeSpan.FromSeconds(_actionTimeout), type, @params).Result;
        }


        public ResponseData DoClusterSetSlot(RequestType type, string action, int slot, string nodeID)
        {
            return DoClusterSetSlotAsync(type, action, slot, nodeID, TimeSpan.FromSeconds(_actionTimeout)).Result;
        }

        /// <summary>
        /// 保持连接
        /// </summary>
        /// <param name="action"></param>
        public void KeepAlived(Action action)
        {
            action();
        }
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Quit()
        {
            if (IsConnected)
            {
                _cnn.Disconnect();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            IsConnected = false;
            _cnn.Dispose();
        }
    }
}
