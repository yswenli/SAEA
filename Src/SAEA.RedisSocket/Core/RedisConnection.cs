/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnection
*版本号： v4.3.2.5
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
*版本号： v4.3.2.5
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.RedisSocket.Base;
using SAEA.RedisSocket.Base.Net;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SAEA.RedisSocket.Core
{
    internal class RedisConnection
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


        public RedisConnection(string ipPort, int actionTimeout = 60)
        {
            this.IPPort = ipPort;
            var address = ipPort.ToIPPort();
            _cnn = new RClient(102400, address.Item1, address.Item2);
            _cnn.OnActived += _cnn_OnActived;
            _cnn.OnMessage += _cnn_OnMessage;
            _cnn.OnDisconnected += _cnn_OnDisconnected;
            _redisCoder = new RedisCoder(actionTimeout);
        }

        private void _cnn_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(this.IPPort);
        }

        private void _cnn_OnActived(DateTime actived)
        {
            _actived = actived;
        }


        protected virtual void _cnn_OnMessage(string command)
        {
            RedisCoder.Enqueue(command);
        }


        /// <summary>
        /// 连接到redisServer
        /// </summary>
        public void Connect()
        {
            lock (_syncLocker)
            {
                if (!IsConnected)
                {
                    var autoResetEvent = new AutoResetEvent(false);

                    _cnn.ConnectAsync((s) =>
                    {
                        if (s == System.Net.Sockets.SocketError.Success)
                        {
                            IsConnected = true;
                        }
                        autoResetEvent.Set();
                    });
                    var result = autoResetEvent.WaitOne(10 * 1000);
                    if (!result || !IsConnected)
                    {
                        _cnn.Disconnect();
                        throw new Exception("无法连接到redis server!");
                    }
                }
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="cmd"></param>
        void Request(string cmd)
        {
            _cnn.Request(Encoding.UTF8.GetBytes(cmd));
        }

        /// <summary>
        /// 发送,
        /// 命令行模式
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        internal ResponseData RequestWithConsole(string cmd)
        {
            ResponseData result = new ResponseData() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    var @params = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    if (@params != null && @params.Length > 0)
                    {
                        var redisCmd = @params[0];

                        if (EnumHelper.GetEnum(redisCmd, out RequestType requestType))
                        {
                            var sendData = RedisCoder.Coder(requestType, @params);
                            Request(sendData);
                            result = RedisCoder.Decoder();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="cmd"></param>
        public ResponseData Do(RequestType type)
        {
            var cmd = RedisCoder.Coder(type, type.ToString());
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.Do, null);
            }
            else if (result.Type == ResponseType.Error)
            {
                throw new Exception(result.Data);
            }
            else
                return result;
        }

        public ResponseData DoInOne(RequestType type, string content)
        {
            var cmd = RedisCoder.Coder(type, type.ToString(), content);
            Request(cmd);
            return RedisCoder.Decoder();
        }


        public ResponseData DoWithKey(RequestType type, string key)
        {
            key.KeyCheck();

            var cmd = RedisCoder.Coder(type, type.ToString(), key);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithKey, type, key);
            }
            else
                return result;
        }

        public ResponseData DoWithKeyValue(RequestType type, string key, string value)
        {
            key.KeyCheck();
            var cmd = RedisCoder.Coder(type, type.ToString(), key, value);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithKeyValue, type, key, value);
            }
            else
                return result;
        }

        public void DoExpire(string key, int seconds)
        {
            key.KeyCheck();
            var cmd = RedisCoder.Coder(RequestType.EXPIRE, RequestType.EXPIRE.ToString(), key, seconds.ToString());
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                OnRedirect.Invoke(result.Data, OperationType.DoExpire, key, seconds);
                return;
            }
        }


        public void DoExpireInsert(RequestType type, string key, string value, int seconds)
        {
            key.KeyCheck();
            var cmd = RedisCoder.Coder(type, type.ToString(), key, value);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                OnRedirect.Invoke(result.Data, OperationType.DoExpireInsert, key, value, seconds);
                return;
            }
            cmd = RedisCoder.Coder(RequestType.EXPIRE, string.Format("{0} {1} {2}", type.ToString(), key, seconds));
            Request(cmd);
            RedisCoder.Decoder();
        }

        public ResponseData DoHash(RequestType type, string id, string key, string value)
        {
            id.KeyCheck();
            key.KeyCheck();
            var cmd = RedisCoder.Coder(type, type.ToString(), id, key, value);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoHash, type, id, key, value);
            }
            else
                return result;
        }

        public ResponseData DoRang(RequestType type, string id, double begin = 0, double end = -1)
        {
            id.KeyCheck();
            var cmd = RedisCoder.Coder(type, type.ToString(), id, begin.ToString(), end.ToString(), "WITHSCORES");
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoRang, type, id, begin, end);
            }
            else
                return result;
        }

        public void DoSub(string[] channels, Action<string, string> onMsg)
        {
            lock (_syncLocker)
            {
                List<string> list = new List<string>();
                list.Add(RequestType.SUBSCRIBE.ToString());
                list.AddRange(channels);
                var cmd = RedisCoder.Coder(RequestType.SUBSCRIBE, list.ToArray());
                Request(cmd);
                RedisCoder.IsSubed = true;
                while (RedisCoder.IsSubed)
                {
                    var result = RedisCoder.Decoder();
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
            }
        }


        public ResponseData DoBatchWithDic(RequestType type, Dictionary<string, string> dic)
        {
            var cmd = RedisCoder.CoderForDic(type, dic);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithDic, type, dic);
            }
            else
                return result;
        }

        public ResponseData DoBatchWithParams(RequestType type, params string[] keys)
        {
            keys.KeyCheck();
            var cmd = RedisCoder.CoderWithParams(type, type.ToString(), keys);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithParams, type, keys);
            }
            else
                return result;
        }


        public ResponseData DoBatchWithIDKeys(RequestType type, string id, params string[] keys)
        {
            id.KeyCheck();
            keys.KeyCheck();

            List<string> list = new List<string>();
            list.Add(type.ToString());
            list.Add(id);
            list.AddRange(keys);
            var cmd = RedisCoder.Coder(type, list.ToArray());
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDKeys, type, id, keys);
            }
            else
                return result;
        }

        public ResponseData DoBatchWithIDDic(RequestType type, string id, Dictionary<double, string> dic)
        {
            id.KeyCheck();
            var cmd = RedisCoder.CoderForDicWidthID(type, id, dic);
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDDic, type, id, dic);
            }
            else
                return result;
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
            var cmd = "";

            if (offset < 0) offset = 0;

            if (!string.IsNullOrEmpty(pattern))
            {
                if (count > -1)
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.MATCH, pattern);
                }
            }
            else
            {
                if (count > -1)
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), offset.ToString());
                }
            }
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ScanResponse)OnRedirect.Invoke(result.Data, OperationType.DoScan, type, offset, pattern, count);
            }
            else
            {
                var scanResponse = new ScanResponse();

                if (result.Type == ResponseType.Lines)
                {
                    return result.ToScanResponse();
                }
                return null;
            }

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
            key.KeyCheck();

            var cmd = "";

            if (offset < 0) offset = 0;

            if (!string.IsNullOrEmpty(pattern))
            {
                if (count > -1)
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.MATCH, pattern);
                }
            }
            else
            {
                if (count > -1)
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = RedisCoder.Coder(type, type.ToString(), key, offset.ToString());
                }
            }
            Request(cmd);
            var result = RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ScanResponse)OnRedirect.Invoke(result.Data, OperationType.DoScanKey, type, key, offset, pattern, count);
            }
            else
            {
                if (result.Type == ResponseType.Lines)
                {
                    return result.ToScanResponse();
                }
                return null;
            }
        }


        public ResponseData DoCluster(RequestType type, params object[] @params)
        {
            lock (_syncLocker)
            {
                List<string> list = new List<string>();

                var arr = type.ToString().Split("_");

                list.AddRange(arr);

                if (@params != null)
                {
                    foreach (var item in @params)
                    {
                        list.Add(item.ToString());
                    }
                }
                var cmd = RedisCoder.Coder(type, list.ToArray());
                Request(cmd);
                var result = RedisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoCluster, type, @params);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
        }


        public ResponseData DoClusterSetSlot(RequestType type, string action, int slot, string nodeID)
        {
            lock (_syncLocker)
            {
                List<string> list = new List<string>();

                var arr = type.ToString().Split("_");

                list.AddRange(arr);

                list.Add(slot.ToString());

                list.Add(action);

                list.Add(nodeID);

                var cmd = RedisCoder.Coder(type, list.ToArray());
                Request(cmd);
                var result = RedisCoder.Decoder();

                if (result.Type == ResponseType.Redirect)
                {
                    return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoClusterSetSlot, type, action, slot, nodeID);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
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
