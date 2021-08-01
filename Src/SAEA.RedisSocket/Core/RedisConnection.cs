/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnection
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Common.Threading;
using SAEA.RedisSocket.Base;
using SAEA.RedisSocket.Base.Net;
using SAEA.RedisSocket.Core.Stream;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// 连接包装类
    /// </summary>
    internal partial class RedisConnection : IDisposable
    {
        RClient _cnn;

        DateTime _actived;

        /// <summary>
        /// 同步对象
        /// </summary>
        public readonly object SyncRoot;

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

        public bool IsDisposed { get; private set; } = false;


        public RedisServerType RedisServerType { get; set; }


        public string IPPort { get; set; }


        public event Action<string> OnDisconnected;


        /// <summary>
        /// 连接转向事件
        /// </summary>
        public event RedirectHandler OnRedirect;


        int _actionTimeout = 6 * 1000;

        /// <summary>
        /// RedisConnection
        /// </summary>
        /// <param name="ipPort"></param>
        /// <param name="actionTimeout"></param>
        public RedisConnection(string ipPort, int actionTimeout = 6 * 1000)
        {
            this.IPPort = ipPort;
            var address = ipPort.ToIPPort();
            _actionTimeout = actionTimeout;
            _cnn = new RClient(102400, address.Item1, address.Item2);
            _cnn.OnActived += _cnn_OnActived;
            _cnn.OnMessage += _cnn_OnMessage;
            _cnn.OnDisconnected += _cnn_OnDisconnected;
            _redisCoder = new RedisCoder(_cnn);
            SyncRoot = _cnn.SyncRoot;
        }

        private void _cnn_OnDisconnected(string ID, Exception ex)
        {
            if (!IsDisposed)
            {
                OnDisconnected?.Invoke(this.IPPort);
                IsConnected = false;
                ThreadHelper.Sleep(1000);
                Connect();
            }
        }

        private async Task _cnn_OnActived(DateTime actived)
        {
            await Task.Run(() => { _actived = actived; });
        }


        protected virtual void _cnn_OnMessage(Memory<byte> msg)
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
        internal ResponseData<string> RequestWithConsole(string cmd)
        {
            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        ResponseData<string> sresult = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

                        if (!string.IsNullOrWhiteSpace(cmd))
                        {
                            var @params = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            if (@params != null && @params.Length > 0)
                            {
                                var redisCmd = @params[0].ToUpper();

                                if (EnumHelper.GetEnum(redisCmd, out RequestType requestType1))
                                {
                                    RedisCoder.RequestOnlyParams(@params);
                                    sresult = RedisCoder.Decoder<string>(requestType1, token);
                                }
                                else
                                {
                                    if (@params.Length > 1)
                                    {
                                        redisCmd = $"{@params[0]}_{@params[1]}".ToUpper();

                                        if (EnumHelper.GetEnum(redisCmd, out RequestType requestType2))
                                        {
                                            RedisCoder.RequestOnlyParams(@params);
                                            sresult = RedisCoder.Decoder<string>(requestType2, token);
                                        }
                                        else
                                        {
                                            sresult.Type = ResponseType.Error;
                                            sresult.Data = "不支持的命令 cmd:" + cmd;
                                        }
                                    }
                                    else
                                    {
                                        sresult.Type = ResponseType.Error;
                                        sresult.Data = "不支持的命令 cmd:" + cmd;
                                    }
                                }
                            }
                        }
                        return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
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
        public ResponseData<string> Do(RequestType type)
        {
            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.RequestOnlyParams(type.ToString());
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.Do, null);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout," + tex.Message;
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

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
        public ResponseData<string> DoWithOne(RequestType type, string content)
        {
            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {

                        content.KeyCheck();
                        RedisCoder.Request(type, content);
                        return RedisCoder.Decoder<string>(type, token);
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        /// <summary>
        /// 用于可以迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ResponseData<string> DoWithKey(RequestType type, string key)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(type, key);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoWithKey, type, key);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        public ResponseData<string> DoWithKeyValue(RequestType type, string key, string value)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                lock (SyncRoot)
                {
                    RedisCoder.Request(type, key, value);
                    var sresult = RedisCoder.Decoder<string>(type, token);
                    if (sresult != null && sresult.Type == ResponseType.Redirect)
                    {
                        return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoWithKeyValue, type, key, value);
                    }
                    else
                        return sresult;
                }
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<string> DoWithID(RequestType type, string id, string key, string value)
        {
            id.KeyCheck();
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(type, id, key, value);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoWithID, type, id, key, value);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<string> DoWithMutiParams(RequestType type, params string[] keys)
        {
            keys.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(type, keys);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchWithParams, type, keys);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// DoStreamSub
        /// </summary>
        /// <param name="type"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public ResponseData<IEnumerable<StreamEntry>> DoStreamSub(RequestType type, params string[] keys)
        {
            keys.KeyCheck();

            ResponseData<IEnumerable<StreamEntry>> result = new ResponseData<IEnumerable<StreamEntry>>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(type, keys);
                        var sresult = RedisCoder.Decoder(token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<IEnumerable<StreamEntry>>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchWithParams, type, keys);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<IEnumerable<IdFiled>> DoStreamRange(params string[] @params)
        {
            @params.KeyCheck();

            ResponseData<IEnumerable<IdFiled>> result = new ResponseData<IEnumerable<IdFiled>>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(RequestType.XRANGE, @params);
                        var sresult = RedisCoder.StreamRangeDecoder(token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<IEnumerable<IdFiled>>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchWithParams, RequestType.XRANGE, @params);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<string> DoExpire(string key, int seconds)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(RequestType.EXPIRE, key, seconds.ToString());
                        var sresult = RedisCoder.Decoder<string>(RequestType.EXPIRE, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            sresult = (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoExpire, key, seconds);
                        }
                        return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        public ResponseData<string> DoExpireAt(string key, int timestamp)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(RequestType.EXPIREAT, key, timestamp.ToString());
                        var sresult = RedisCoder.Decoder<string>(RequestType.EXPIREAT, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            sresult = (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoExpireAt, key, timestamp);
                        }
                        return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<string> DoExpireInsert(RequestType type, string key, string value, int seconds)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(type, key, value);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoExpireInsert, key, value, seconds);
                        }
                        RedisCoder.Request(RequestType.EXPIRE, string.Format("{0} {1}", key, seconds));
                        return RedisCoder.Decoder<string>(RequestType.EXPIRE, token);
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        public ResponseData<string> DoRang(RequestType type, string key, double begin = 0, double end = -1)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.Request(type, key, begin.ToString(), end.ToString(), "WITHSCORES");
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoRang, type, key, begin, end);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        public ResponseData<string> DoRangByScore(RequestType type, string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20, bool withScore = false)
        {
            key.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.RequestForRandByScore(type, key, min, max, rangType, offset, count, withScore);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoRangByScore, type, key, min, max, rangType, offset, count, withScore);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        public void DoSub(string[] channels, Action<string, string> onMsg)
        {
            RedisCoder.Request(RequestType.SUBSCRIBE, channels);

            RedisCoder.IsSubed = true;

            TaskHelper.Run(() =>
            {
                lock (SyncRoot)
                {
                    while (RedisCoder.IsSubed)
                    {
                        var result = RedisCoder.Decoder<string>(RequestType.SUBSCRIBE, System.Threading.CancellationToken.None);
                        if (result != null && result.Type == ResponseType.Sub)
                        {
                            var arr = result.Data.ToArray(false, Environment.NewLine);
                            onMsg.Invoke(arr[0], arr[1]);
                        }
                        else if (result != null && result.Type == ResponseType.UnSub)
                        {
                            break;
                        }
                    }
                }
            });
        }

        public ResponseData<string> DoMultiLineWithList(RequestType type, string id, IEnumerable<string> list)
        {
            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };

            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.RequestForList(type, id, list);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchWithList, type, list);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<string> DoMultiLineWithDic(RequestType type, Dictionary<string, string> dic)
        {
            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.RequestForDic(type, dic);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchWithDic, type, dic);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }


        public ResponseData<string> DoBatchWithIDKeys(RequestType type, string id, params string[] keys)
        {
            id.KeyCheck();
            keys.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {

                        List<string> list = new List<string>();
                        list.Add(type.ToString());
                        list.Add(id);
                        list.AddRange(keys);
                        RedisCoder.RequestOnlyParams(list.ToArray());
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchWithIDKeys, type, id, keys);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;
        }

        public ResponseData<string> DoBatchZaddWithIDDic(RequestType type, string id, Dictionary<double, string> dic)
        {
            id.KeyCheck();
            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {

                        RedisCoder.RequestForDicWidthID(type, id, dic);
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoBatchZaddWithIDDic, type, id, dic);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }

        public ResponseData<string> DoBatchWithIDDic(RequestType type, string id, Dictionary<string, string> dic)
        {
            id.KeyCheck();

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        RedisCoder.RequestForDicWidthID(type, id, dic);
                        var result = RedisCoder.Decoder<string>(type, token);
                        if (result != null && result.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDDic, type, id, dic);
                        }
                        else
                            return result;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
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
            return TaskHelper.Run((token) =>
            {
                lock (SyncRoot)
                {
                    if (offset < 0) offset = 0;

                    if (!string.IsNullOrEmpty(pattern))
                    {
                        if (count > -1)
                        {
                            RedisCoder.Request(type, offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                        }
                        else
                        {
                            RedisCoder.Request(type, offset.ToString(), RedisConst.MATCH, pattern);
                        }
                    }
                    else
                    {
                        if (count > -1)
                        {
                            RedisCoder.Request(type, offset.ToString(), RedisConst.COUNT, count.ToString());
                        }
                        else
                        {
                            RedisCoder.Request(type, offset.ToString());
                        }
                    }
                    var sresult = RedisCoder.Decoder<string>(type, token);

                    if (sresult == null) return null;

                    if (sresult.Type == ResponseType.Redirect)
                    {
                        return (ScanResponse)OnRedirect.Invoke(sresult.Data, OperationType.DoScan, type, offset, pattern, count);
                    }
                    else
                    {
                        if (sresult.Type == ResponseType.Lines)
                        {
                            return sresult.ToScanResponse();
                        }
                        return null;
                    }
                }
            }, _actionTimeout).Result;
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

            return TaskHelper.Run((token) =>
            {
                lock (SyncRoot)
                {
                    if (offset < 0) offset = 0;

                    if (!string.IsNullOrEmpty(pattern))
                    {
                        if (count > -1)
                        {
                            RedisCoder.Request(type, key, offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                        }
                        else
                        {
                            RedisCoder.Request(type, key, offset.ToString(), RedisConst.MATCH, pattern);
                        }
                    }
                    else
                    {
                        if (count > -1)
                        {
                            RedisCoder.Request(type, key, offset.ToString(), RedisConst.COUNT, count.ToString());
                        }
                        else
                        {
                            RedisCoder.Request(type, key, offset.ToString());
                        }
                    }

                    var result = RedisCoder.Decoder<string>(type, token);

                    if (result == null) return null;

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
            }, _actionTimeout).Result;
        }


        public ResponseData<string> DoMutiCmd(RequestType type, params object[] @params)
        {

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
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
                        RedisCoder.RequestOnlyParams(list.ToArray());
                        var sresult = RedisCoder.Decoder<string>(type, token);
                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoCluster, type, @params);
                        }
                        else if (sresult.Type == ResponseType.Error)
                        {
                            throw new Exception(sresult.Data);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

        }


        public ResponseData<string> DoClusterSetSlot(RequestType type, string action, int slot, string nodeID)
        {

            ResponseData<string> result = new ResponseData<string>() { Type = ResponseType.Empty, Data = "未知的命令" };
            try
            {
                result = TaskHelper.Run((token) =>
                {
                    lock (SyncRoot)
                    {
                        List<string> list = new List<string>();

                        var arr = type.ToString().Split("_");

                        list.AddRange(arr);

                        list.Add(slot.ToString());

                        list.Add(action);

                        list.Add(nodeID);

                        RedisCoder.RequestOnlyParams(list.ToArray());

                        var sresult = RedisCoder.Decoder<string>(type, token);

                        if (sresult != null && sresult.Type == ResponseType.Redirect)
                        {
                            return (ResponseData<string>)OnRedirect.Invoke(sresult.Data, OperationType.DoClusterSetSlot, type, action, slot, nodeID);
                        }
                        else if (sresult != null && sresult.Type == ResponseType.Error)
                        {
                            throw new Exception(sresult.Data);
                        }
                        else
                            return sresult;
                    }
                }, _actionTimeout).Result;
            }
            catch (TaskCanceledException tex)
            {
                result.Type = ResponseType.Error;
                result.Data = "Action Timeout";
            }
            catch (Exception ex)
            {
                result.Type = ResponseType.Error;
                result.Data = ex.Message;
            }
            return result;

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
            IsDisposed = true;
            _cnn.Dispose();
            
        }
    }
}
