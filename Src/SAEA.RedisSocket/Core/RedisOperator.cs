using SAEA.Commom;
using SAEA.RedisSocket.Model;
using SAEA.RedisSocket.Net;
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
                return _redisCoder.Decoder();
            }
        }

        public ResponseData Do(RequestType type, string key)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key);
                _cnn.Send(cmd);
                return _redisCoder.Decoder();
            }
        }

        public ResponseData Do(RequestType type, string key, string value)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
                _cnn.Send(cmd);
                return _redisCoder.Decoder();
            }
        }

        public void DoExpire(string key, int seconds)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(RequestType.EXPIRE, RequestType.EXPIRE.ToString(), key, seconds.ToString());
                _cnn.Send(cmd);
                _redisCoder.Decoder();
            }
        }

        public void DoExpire(RequestType type, string key, string value, int seconds)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
                _cnn.Send(cmd);
                _redisCoder.Decoder();

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
                return _redisCoder.Decoder();
            }
        }
        public ResponseData Do(RequestType type, string id, double begin = 0, double end = -1)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), id, begin.ToString(), end.ToString(), " WITHSCORES");
                _cnn.Send(cmd);
                return _redisCoder.Decoder();
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
                return _redisCoder.Decoder();
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
                return _redisCoder.Decoder();
            }
        }


    }
}
