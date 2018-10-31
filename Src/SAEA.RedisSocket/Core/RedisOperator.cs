/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisOperator
*版本号： V3.2.1.1
*唯一标识：23cf910b-3bed-4d80-9e89-92c04fba1e5e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*修改时间：2018/10/22 14:12:40
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/22 14:12:40
*修改人： yswenli
*版本号： V3.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Interface;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis的编解码操作类
    /// </summary>
    public abstract class RedisOperator
    {
        IRedisConnection _cnn;

        RedisCoder _redisCoder;

        string space = " ";

        object _syncLocker = new object();


        /// <summary>
        /// 连接转向事件
        /// </summary>
        public event Func<string, IRedisConnection> OnRedirect;

        public RedisOperator(IRedisConnection cnn)
        {
            _cnn = cnn;
            _redisCoder = _cnn.RedisCoder;
        }

        public ResponseData Do(RequestType type)
        {
            var cmd = _redisCoder.Coder(type, string.Format("{0}", type.ToString()));
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return Do(type);
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
            var cmd = _redisCoder.Coder(type, type.ToString(), content);
            _cnn.Send(cmd);
            return _redisCoder.Decoder();
        }



        public ResponseData Do1(RequestType type, string key, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(key);
                    _redisCoder = _cnn.RedisCoder;
                }
            var cmd = _redisCoder.Coder(type, type.ToString(), key);
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return Do1(type, key, false);
            }
            else
                return result;
        }

        public ResponseData Do2(RequestType type, string key, string value, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(key);
                    _redisCoder = _cnn.RedisCoder;
                }
            var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return Do2(type, key, value, false);
            }
            else
                return result;
        }

        public void DoExpire(string key, int seconds, bool hasCalc)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(key);
                    _redisCoder = _cnn.RedisCoder;
                }

            var cmd = _redisCoder.Coder(RequestType.EXPIRE, RequestType.EXPIRE.ToString(), key, seconds.ToString());
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                DoExpire(key, seconds, false);
            }
        }


        public void DoExpireInsert(RequestType type, string key, string value, int seconds)
        {
            if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
            {
                _cnn = RedisConnectionManager.GetConnectionBySlot(key);
                _redisCoder = _cnn.RedisCoder;
            }
            var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;

                cmd = _redisCoder.Coder(type, type.ToString(), key, value);
                _cnn.Send(cmd);
                _redisCoder.Decoder();
            }
            cmd = _redisCoder.Coder(RequestType.EXPIRE, string.Format("{0} {1} {2}", type.ToString(), key, seconds));
            _cnn.Send(cmd);
            _redisCoder.Decoder();
        }

        public ResponseData Do3(RequestType type, string id, string key, string value, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(id);
                    _redisCoder = _cnn.RedisCoder;
                }
            var cmd = _redisCoder.Coder(type, type.ToString(), id, key, value);
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return Do3(type, id, key, value, false);
            }
            else
                return result;
        }
        public ResponseData DoRang(RequestType type, string id, double begin = 0, double end = -1, bool hasCalc = true)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(id);
                    _redisCoder = _cnn.RedisCoder;
                }
            var cmd = _redisCoder.Coder(type, type.ToString(), id, begin.ToString(), end.ToString(), "WITHSCORES");
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoRang(type, id, begin, end, false);
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
                var cmd = _redisCoder.Coder(RequestType.SUBSCRIBE, sb.ToString());
                _cnn.Send(cmd);
                _redisCoder.IsSubed = true;
                while (_redisCoder.IsSubed)
                {
                    var result = _redisCoder.Decoder();
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


        public ResponseData DoBatch(RequestType type, Dictionary<string, string> dic)
        {
            List<string> list = new List<string>();
            foreach (var item in dic)
            {
                list.Add(item.Key);
                list.Add(item.Value);
            }
            var cmd = _redisCoder.Coder(type, list.ToArray());
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoBatch(type, dic);
            }
            else
                return result;
        }

        public ResponseData DoBatch1(RequestType type, params string[] keys)
        {
            var cmd = _redisCoder.Coder(type, keys);
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoBatch1(type, keys);
            }
            else
                return result;
        }


        public ResponseData DoBatch2(RequestType type, string id, params string[] keys)
        {
            List<string> list = new List<string>();
            list.Add(type.ToString());
            list.Add(id);
            list.AddRange(keys);
            var cmd = _redisCoder.Coder(type, list.ToArray());
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoBatch2(type, id, keys);
            }
            else
                return result;
        }

        public ResponseData DoBatch3(RequestType type, string id, Dictionary<double, string> dic, bool hasCalc)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(id);
                    _redisCoder = _cnn.RedisCoder;
                }
            List<string> list = new List<string>();
            list.Add(type.ToString());
            list.Add(id);
            foreach (var item in dic)
            {
                list.Add(item.Key.ToString());
                list.Add(item.Value);
            }
            var cmd = _redisCoder.Coder(type, list.ToArray());
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoBatch3(type, id, dic, false);
            }
            else
                return result;
        }

        public ResponseData DoBatch4<T>(RequestType type, string id, Dictionary<string, T> dic, bool hasCalc)
        {
            if (hasCalc)
                if (_cnn.RedisServerType == RedisServerType.ClusterMaster || _cnn.RedisServerType == RedisServerType.ClusterSlave)
                {
                    _cnn = RedisConnectionManager.GetConnectionBySlot(id);
                    _redisCoder = _cnn.RedisCoder;
                }

            List<string> list = new List<string>();
            list.Add(type.ToString());
            list.Add(id);
            foreach (var item in dic)
            {
                list.Add(item.Key);
                list.Add(SerializeHelper.Serialize(item.Value));
            }
            var cmd = _redisCoder.Coder(type, list.ToArray());
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoBatch4<T>(type, id, dic, false);
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

        public ScanResponse Do(RequestType type, int offset = 0, string pattern = "*", int count = -1)
        {
            var cmd = "";

            if (offset < 0) offset = 0;

            if (!string.IsNullOrEmpty(pattern))
            {
                if (count > -1)
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.MATCH, pattern);
                }
            }
            else
            {
                if (count > -1)
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString(), RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString());
                }
            }
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return Do(type, offset, pattern, count);
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
                    _cnn = RedisConnectionManager.GetConnectionBySlot(key);
                    _redisCoder = _cnn.RedisCoder;
                }

            var cmd = "";

            if (offset < 0) offset = 0;

            if (!string.IsNullOrEmpty(pattern))
            {
                if (count > -1)
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.MATCH, pattern, RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.MATCH, pattern);
                }
            }
            else
            {
                if (count > -1)
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString(), RedisConst.COUNT, count.ToString());
                }
                else
                {
                    cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString());
                }
            }
            _cnn.Send(cmd);
            var result = _redisCoder.Decoder();
            if (result.Type == ResponseType.Redirect)
            {
                _cnn = OnRedirect.Invoke(result.Data);
                _redisCoder = _cnn.RedisCoder;
                return DoScanKey(type, key, offset, pattern, count, false);
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
                var cmd = _redisCoder.Coder(type, list.ToArray());
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return Do(type);
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

                var cmd = _redisCoder.Coder(type, list.ToArray());
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return Do(type);
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
