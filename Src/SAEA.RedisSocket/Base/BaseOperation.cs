/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Base
*文件名： BaseOperation
*版本号： V3.2.1.1
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
*版本号： V3.2.1.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Base
{
    public abstract class BaseOperation
    {
        RedisConnection _cnn;

        string space = " ";

        object _syncLocker = new object();


        /// <summary>
        /// 连接转向事件
        /// </summary>
        public event RedirectHandler OnRedirect;

        public BaseOperation(RedisConnection cnn)
        {
            _cnn = cnn;
        }

        public ResponseData Do(RequestType type)
        {
            var cmd = _cnn.RedisCoder.Coder(type, string.Format("{0}", type.ToString()));
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
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
            var cmd = _cnn.RedisCoder.Coder(type, type.ToString(), content);
            _cnn.Request(cmd);
            return _cnn.RedisCoder.Decoder();
        }



        public ResponseData DoWithKey(RequestType type, string key, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    return (ResponseData)OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(key), OperationType.DoWithKey, type, key, false);
                }
            var cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithKey, type, key, false);
            }
            else
                return result;
        }

        public ResponseData DoWithKeyValue(RequestType type, string key, string value, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    return (ResponseData)OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(key), OperationType.DoWithKeyValue, type, key, value, false);
                }
            var cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key, value);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoWithKeyValue, type, key, value, false);
            }
            else
                return result;
        }

        public void DoExpire(string key, int seconds, bool hasCalc)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(key), OperationType.DoExpire, key, seconds, false);
                    return;
                }

            var cmd = _cnn.RedisCoder.Coder(RequestType.EXPIRE, RequestType.EXPIRE.ToString(), key, seconds.ToString());
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                OnRedirect.Invoke(result.Data, OperationType.DoExpire, key, seconds, false);
                return;
            }
        }


        public void DoExpireInsert(RequestType type, string key, string value, int seconds)
        {
            if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
            {
                OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(key), OperationType.DoExpireInsert, key, value, seconds, false);
                return;
            }
            var cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key, value);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                OnRedirect.Invoke(result.Data, OperationType.DoExpireInsert, key, value, seconds, false);
                return;
            }
            cmd = _cnn.RedisCoder.Coder(RequestType.EXPIRE, string.Format("{0} {1} {2}", type.ToString(), key, seconds));
            _cnn.Request(cmd);
            _cnn.RedisCoder.Decoder();
        }

        public ResponseData DoHash(RequestType type, string id, string key, string value, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    return (ResponseData)OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(key), OperationType.DoHash, type, id, key, value, false);
                }
            var cmd = _cnn.RedisCoder.Coder(type, type.ToString(), id, key, value);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoHash, type, id, key, value, false);
            }
            else
                return result;
        }

        public ResponseData DoRang(RequestType type, string id, double begin = 0, double end = -1, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    return (ResponseData)OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(id), OperationType.DoRang, type, id, begin, end, false);
                }
            var cmd = _cnn.RedisCoder.Coder(type, type.ToString(), id, begin.ToString(), end.ToString(), "WITHSCORES");
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoRang, type, id, begin, end, false);
            }
            else
                return result;
        }

        public void DoSub(string[] channels, Action<string, string> onMsg)
        {
            lock (_syncLocker)
            {
                var sb = new StringBuilder();
                sb.Append(RequestType.SUBSCRIBE.ToString());
                for (int i = 0; i < channels.Length; i++)
                {
                    sb.Append(space + channels[i]);
                }
                var cmd = _cnn.RedisCoder.Coder(RequestType.SUBSCRIBE, sb.ToString());
                _cnn.Request(cmd);
                _cnn.RedisCoder.IsSubed = true;
                while (_cnn.RedisCoder.IsSubed)
                {
                    var result = _cnn.RedisCoder.Decoder();
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
            var cmd = _cnn.RedisCoder.CoderForDic(type, dic);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithDic, type, dic);
            }
            else
                return result;
        }

        public ResponseData DoBatchWithParams(RequestType type, params string[] keys)
        {
            var cmd = _cnn.RedisCoder.Coder(type, keys);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithParams, type, keys);
            }
            else
                return result;
        }


        public ResponseData DoBatchWithIDKeys(RequestType type, string id, params string[] keys)
        {
            List<string> list = new List<string>();
            list.Add(type.ToString());
            list.Add(id);
            list.AddRange(keys);
            var cmd = _cnn.RedisCoder.Coder(type, list.ToArray());
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDKeys, type, id, keys);
            }
            else
                return result;
        }

        public ResponseData DoBatchWithIDDic(RequestType type, string id, Dictionary<double, string> dic, bool hasCalc)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    return (ResponseData)OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(id), OperationType.DoBatchWithIDDic, type, id, dic, false);
                }

            var cmd = _cnn.RedisCoder.CoderForDicWidthID(type, id, dic);
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ResponseData)OnRedirect.Invoke(result.Data, OperationType.DoBatchWithIDDic, type, id, dic, false);
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
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.MATCH, pattern);
                }
            }
            else
            {
                if (count > -1)
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), offset.ToString());
                }
            }
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
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
        public ScanResponse DoScanKey(RequestType type, string key, int offset = 0, string pattern = "*", int count = -1, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    return (ScanResponse)OnRedirect.Invoke(RedisConnectionManager.GetIPPortWidthKey(key), OperationType.DoScanKey, type, key, offset, pattern, count, false);
                }

            var cmd = "";

            if (offset < 0) offset = 0;

            if (!string.IsNullOrEmpty(pattern))
            {
                if (count > -1)
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.MATCH, pattern);
                }
            }
            else
            {
                if (count > -1)
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _cnn.RedisCoder.Coder(type, type.ToString(), key, offset.ToString());
                }
            }
            _cnn.Request(cmd);
            var result = _cnn.RedisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                return (ScanResponse)OnRedirect.Invoke(result.Data, OperationType.DoScanKey, type, key, offset, pattern, count, false);
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
                var cmd = _cnn.RedisCoder.Coder(type, list.ToArray());
                _cnn.Request(cmd);
                var result = _cnn.RedisCoder.Decoder();
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

                var cmd = _cnn.RedisCoder.Coder(type, list.ToArray());
                _cnn.Request(cmd);
                var result = _cnn.RedisCoder.Decoder();
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
    }
}
