/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnectionAsync
*版本号： v26.4.23.1
*唯一标识：6f4b7cc8-9ee1-421c-8022-7d13e6b97355
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/06/18 14:45:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2020/06/18 14:45:01
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common.Threading;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
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
        internal async Task<ResponseData<string>> RequestWithConsoleAsync(string cmd, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return RequestWithConsole(cmd);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<ResponseData<string>> DoAsync(RequestType type, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return Do(type);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        /// <summary>
        /// 用于不会迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="content"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<ResponseData<string>> DoWithOneAsync(RequestType type, string content, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoWithOne(type, content);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        /// <summary>
        /// 用于可以迁移的命令
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<ResponseData<string>> DoWithKeyAsync(RequestType type, string key, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoWithKey(type, key);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData<string>> DoWithKeyValueAsync(RequestType type, string key, string value, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoWithKeyValue(type, key, value);
            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData<string>> DoWithIDAsync(RequestType type, string id, string key, string value, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoWithID(type, id, key, value);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData<string>> DoWithMutiParamsAsync(RequestType type, TimeSpan timeSpan, params string[] keys)
        {
            return await TaskHelper.Run(() =>
            {
                return DoWithMutiParams(type, keys);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);

        }

        public Task DoExpireAsync(string key, int seconds)
        {
            return TaskHelper.Run(() =>
            {
                DoExpire(key, seconds);
            });
        }

        public Task DoExpireAtAsync(string key, int timestamp)
        {
            return TaskHelper.Run(() =>
            {
                DoExpireAt(key, timestamp);
            });
        }

        public Task DoExpireInsertAsync(RequestType type, string key, string value, int seconds)
        {
            return TaskHelper.Run(() =>
            {
                DoExpireInsert(type, key, value, seconds);
            });
        }

        public async Task<ResponseData<string>> DoRangAsync(RequestType type, string key, double begin, double end, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoRang(type, key, begin, end);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData<string>> DoRangByScoreAsync(TimeSpan timeSpan, RequestType type, string key, double min = double.MinValue, double max = double.MaxValue, RangType rangType = RangType.None, long offset = -1, int count = 20, bool withScore = false)
        {
            return await TaskHelper.Run(() =>
            {
                return DoRangByScore(type, key, min, max, rangType, offset, count, withScore);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData<string>> DoBatchWithListAsync(RequestType type, string id, IEnumerable<string> list, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoMultiLineWithList(type, id, list);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData<string>> DoBatchWithDicAsync(RequestType type, Dictionary<string, string> dic, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoMultiLineWithDic(type, dic);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData<string>> DoBatchWithIDKeysAsync(TimeSpan timeSpan, RequestType type, string id, params string[] keys)
        {
            return await TaskHelper.Run(() =>
            {
                return DoBatchWithIDKeys(type, id, keys);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData<string>> DoBatchZaddWithIDDicAsync(RequestType type, string id, Dictionary<double, string> dic, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoBatchZaddWithIDDic(type, id, dic);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData<string>> DoBatchWithIDDicAsync(RequestType type, string id, Dictionary<string, string> dic, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoBatchWithIDDic(type, id, dic);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ScanResponse> DoScanAsync(TimeSpan timeSpan, RequestType type, int offset = 0, string pattern = "*", int count = -1)
        {
            return await TaskHelper.Run(() =>
            {
                return DoScan(type, offset, pattern, count);

            }).WithCancellationTimeout(timeSpan);
        }

        public async Task<ScanResponse> DoScanKeyAsync(TimeSpan timeSpan, RequestType type, string key, int offset = 0, string pattern = "*", int count = -1)
        {
            return await TaskHelper.Run(() =>
            {
                return DoScanKey(type, key, offset, pattern, count);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }


        public async Task<ResponseData<string>> DoMutiCmdAsync(TimeSpan timeSpan, RequestType type, params object[] @params)
        {
            return await TaskHelper.Run(() =>
            {
                return DoMutiCmd(type, @params);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);
        }

        public async Task<ResponseData<string>> DoClusterSetSlotAsync(RequestType type, string action, int slot, string nodeID, TimeSpan timeSpan)
        {
            return await TaskHelper.Run(() =>
            {
                return DoClusterSetSlot(type, action, slot, nodeID);

            }).WithCancellationTimeout(timeSpan).ConfigureAwait(false);

        }
    }
}
