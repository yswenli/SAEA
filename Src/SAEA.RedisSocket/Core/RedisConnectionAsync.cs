/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisConnectionAsync
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/16 16:24:47
*描述：
*=====================================================================
*修改时间：2020/6/16 16:24:47
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// 连接包装类,RedisConnectionAsync
    /// </summary>
    partial class RedisConnection
    {
        /// <summary>
        /// 连接到redisServer
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                if (!IsConnected)
                {
                    _cnn.Connect();
                    IsConnected = true;
                }
                return IsConnected;

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        /// <summary>
        /// 发送,
        /// 命令行模式
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        internal async Task<ResponseData> RequestWithConsoleAsync(string cmd, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    ResponseData result = new ResponseData() { Type = ResponseType.Empty, Data = "未知的命令" };
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(cmd))
                        {
                            var @params = cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            if (@params != null && @params.Length > 0)
                            {
                                var redisCmd = @params[0].ToUpper();

                                if (EnumHelper.GetEnum(redisCmd, out RequestType requestType1))
                                {
                                    RedisCoder.RequestOnlyParams(@params);
                                    result = RedisCoder.Decoder(requestType1);
                                }
                                else
                                {
                                    redisCmd = $"{@params[0]}_{@params[1]}".ToUpper();

                                    if (EnumHelper.GetEnum(redisCmd, out RequestType requestType2))
                                    {
                                        RedisCoder.RequestOnlyParams(@params);
                                        result = RedisCoder.Decoder(requestType2);
                                    }
                                    else
                                    {
                                        result.Type = ResponseType.Error;
                                        result.Data = "未知的命令 cmd:" + cmd;
                                    }
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

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<ResponseData> DoAsync(RequestType type, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    RedisCoder.RequestOnlyParams(type.ToString());
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.Do, null);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        /// <summary>
        /// 用于不会迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="content"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<ResponseData> DoWithOneAsync(RequestType type, string content, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    content.KeyCheck();
                    RedisCoder.Request(type, content);
                    return RedisCoder.Decoder(type);
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        /// <summary>
        /// 用于可以迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<ResponseData> DoWithKeyAsync(RequestType type, string key, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.Request(type, key);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithKey, type, key);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData> DoWithKeyValueAsync(RequestType type, string key, string value, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.Request(type, key, value);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithKeyValue, type, key, value);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData> DoWithIDAsync(RequestType type, string id, string key, string value, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    id.KeyCheck();
                    key.KeyCheck();
                    RedisCoder.Request(type, id, key, value);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithID, type, id, key, value);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData> DoWithMutiParamsAsync(RequestType type, TimeSpan timeSpan, params string[] keys)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    keys.KeyCheck();
                    RedisCoder.Request(type, keys);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithParams, type, keys);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);

        }

        public Task DoExpireAsync(string key, int seconds)
        {
            return TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.Request(RequestType.EXPIRE, key, seconds.ToString());
                    var result = RedisCoder.Decoder(RequestType.EXPIRE);
                    if (result.Type == ResponseType.Redirect)
                    {
                        OnRedirect.Invoke(result.Data, OperationType.DoExpire, key, seconds);
                        return;
                    }
                }
            });
        }

        public Task DoExpireAtAsync(string key, int timestamp)
        {
            return TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.Request(RequestType.EXPIREAT, key, timestamp.ToString());
                    var result = RedisCoder.Decoder(RequestType.EXPIREAT);
                    if (result.Type == ResponseType.Redirect)
                    {
                        OnRedirect.Invoke(result.Data, OperationType.DoExpireAt, key, timestamp);
                        return;
                    }
                }

            });
        }

        public Task DoExpireInsertAsync(RequestType type, string key, string value, int seconds)
        {
            return TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.Request(type, key, value);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        OnRedirect.Invoke(result.Data, OperationType.DoExpireInsert, key, value, seconds);
                        return;
                    }
                    RedisCoder.Request(RequestType.EXPIRE, string.Format("{0} {1}", key, seconds));
                    RedisCoder.Decoder(RequestType.EXPIRE);
                }
            });
        }

        public async Task<ResponseData> DoRangAsync(RequestType type, string key, double begin, double end, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.Request(type, key, begin.ToString(), end.ToString(), "WITHSCORES");
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoRang, type, key, begin, end);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData> DoRangByScoreAsync(TimeSpan timeSpan, RequestType type, string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20, bool withScore = false)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();
                    RedisCoder.RequestForRandByScore(type, key, min, max, rangType, offset, count, withScore);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoRangByScore, type, key, min, max, rangType, offset, count, withScore);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData> DoBatchWithListAsync(RequestType type, string id, IEnumerable<string> list, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    RedisCoder.RequestForList(type, id, list);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithList, type, list);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData> DoBatchWithDicAsync(RequestType type, Dictionary<string, string> dic, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    RedisCoder.RequestForDic(type, dic);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithDic, type, dic);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData> DoBatchWithIDKeysAsync(TimeSpan timeSpan, RequestType type, string id, params string[] keys)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    id.KeyCheck();
                    keys.KeyCheck();

                    List<string> list = new List<string>();
                    list.Add(type.ToString());
                    list.Add(id);
                    list.AddRange(keys);
                    RedisCoder.RequestOnlyParams(list.ToArray());
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDKeys, type, id, keys);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData> DoBatchZaddWithIDDicAsync(RequestType type, string id, Dictionary<double, string> dic, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    id.KeyCheck();
                    RedisCoder.RequestForDicWidthID(type, id, dic);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchZaddWithIDDic, type, id, dic);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData> DoBatchWithIDDicAsync(RequestType type, string id, Dictionary<string, string> dic, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    id.KeyCheck();
                    RedisCoder.RequestForDicWidthID(type, id, dic);
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDDic, type, id, dic);
                    }
                    else
                        return result;
                }

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ScanResponse> DoScanAsync(TimeSpan timeSpan, RequestType type, int offset = 0, string pattern = "*", int count = -1)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
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
                    var result = RedisCoder.Decoder(type);
                    if (result.Type == ResponseType.Redirect)
                    {
                        return (ScanResponse)OnRedirect.Invoke(result.Data, OperationType.DoScan, type, offset, pattern, count);
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

            }).WithCancellationTimeout(timeSpan);
        }

        public async Task<ScanResponse> DoScanKeyAsync(TimeSpan timeSpan, RequestType type, string key, int offset = 0, string pattern = "*", int count = -1)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    key.KeyCheck();

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
                    var result = RedisCoder.Decoder(type);
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

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData> DoMutiCmdAsync(TimeSpan timeSpan, RequestType type, params object[] @params)
        {
            return await TaskHelper.Run(() =>
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
                    RedisCoder.RequestOnlyParams(list.ToArray());
                    var result = RedisCoder.Decoder(type);
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

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData> DoClusterSetSlotAsync(RequestType type, string action, int slot, string nodeID, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                lock (_syncLocker)
                {
                    List<string> list = new List<string>();

                    var arr = type.ToString().Split("_");

                    list.AddRange(arr);

                    list.Add(slot.ToString());

                    list.Add(action);

                    list.Add(nodeID);

                    RedisCoder.RequestOnlyParams(list.ToArray());

                    var result = RedisCoder.Decoder(type);

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

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);

        }
    }
}
