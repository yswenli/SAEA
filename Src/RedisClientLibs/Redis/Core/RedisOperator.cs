using SAEA.Common;
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
        RedisConnection _cnn;
        RedisCoder _redisCoder;
        object _syncLocker = new object();
        string space = " ";

        const string MATCH = "MATCH";

        const string COUNT = "COUNT";

        public event Func<string, RedisConnection> OnRedirect;

        public RedisOperator(RedisConnection cnn)
        {
            _cnn = cnn;
            _redisCoder = _cnn.RedisCoder;
        }

        public ResponseData Do(RequestType type)
        {
            lock (_syncLocker)
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
        }

        public ResponseData Do(RequestType type, string key)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key);
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return Do(type, key);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
        }

        public ResponseData Do(RequestType type, string key, string value)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return Do(type, key, value);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
        }

        public void DoExpire(string key, int seconds)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(RequestType.EXPIRE, RequestType.EXPIRE.ToString(), key, seconds.ToString());
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    DoExpire(key, seconds);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
            }
        }

        public void DoExpire(RequestType type, string key, string value, int seconds)
        {
            lock (_syncLocker)
            {
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
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }

                cmd = _redisCoder.Coder(RequestType.EXPIRE, string.Format("{0} {1} {2}", type.ToString(), key, seconds));
                _cnn.Send(cmd);
                _redisCoder.Decoder();
            }
        }

        public ResponseData Do(RequestType type, string id, string key, string value)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), id, key, value);
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return Do(type, id, key, value);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
        }
        public ResponseData Do(RequestType type, string id, double begin = 0, double end = -1)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), id, begin.ToString(), end.ToString(), " WITHSCORES");
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return Do(type, id, begin, end);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
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
        public ResponseData DoBatch(RequestType type, string id, params string[] keys)
        {
            lock (_syncLocker)
            {
                var sb = new StringBuilder();
                sb.Append(type.ToString());
                sb.Append(space + id);
                for (int i = 0; i < keys.Length; i++)
                {
                    sb.Append(space + keys[i]);
                }
                var cmd = _redisCoder.Coder(type, sb.ToString());
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return DoBatch(type, id, keys);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
                }
                else
                    return result;
            }
        }

        public ResponseData DoBatch<T>(RequestType type, string id, Dictionary<string, T> dic)
        {
            lock (_syncLocker)
            {
                var sb = new StringBuilder();
                sb.Append(type.ToString());
                sb.Append(space + id);

                foreach (var key in dic.Keys)
                {
                    sb.Append(space + key + space + dic[key]);
                }

                var cmd = _redisCoder.Coder(type, sb.ToString());
                _cnn.Send(cmd);
                var result = _redisCoder.Decoder();
                if (result.Type == ResponseType.Redirect)
                {
                    _cnn = OnRedirect.Invoke(result.Data);
                    _redisCoder = _cnn.RedisCoder;
                    return DoBatch<T>(type, id, dic);
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
        /// SCAN
        /// </summary>
        /// <param name="type"></param>
        /// <param name="offset"></param>
        /// <param name="pattern"></param>
        /// <param name="count"></param>
        /// <returns></returns>

        public ScanResponse Do(RequestType type, int offset = 0, string pattern = "*", int count = -1)
        {
            lock (_syncLocker)
            {
                var cmd = "";

                if (offset < 0) offset = 0;

                if (!string.IsNullOrEmpty(pattern))
                {
                    if (count > -1)
                    {
                        cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString(), MATCH, pattern, COUNT, count.ToString());
                    }
                    else
                    {
                        cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString(), MATCH, pattern);
                    }
                }
                else
                {
                    if (count > -1)
                    {
                        cmd = _redisCoder.Coder(type, type.ToString(), offset.ToString(), COUNT, count.ToString());
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
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
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
        public ScanResponse Do(RequestType type, string key, int offset = 0, string pattern = "*", int count = -1)
        {
            lock (_syncLocker)
            {
                var cmd = "";

                if (offset < 0) offset = 0;

                if (!string.IsNullOrEmpty(pattern))
                {
                    if (count > -1)
                    {
                        cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString(), MATCH, pattern, COUNT, count.ToString());
                    }
                    else
                    {
                        cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString(), MATCH, pattern);
                    }
                }
                else
                {
                    if (count > -1)
                    {
                        cmd = _redisCoder.Coder(type, type.ToString(), key, offset.ToString(), COUNT, count.ToString());
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
                    return Do(type, offset, pattern, count);
                }
                else if (result.Type == ResponseType.Error)
                {
                    throw new Exception(result.Data);
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
        }

    }
}
