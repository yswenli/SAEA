using SAEA.Commom;
using SAEA.RedisSocket.Net;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket
{
    public class RedisCodeOperator
    {
        IOCPClient _client;
        RedisCoder _redisCoder;
        object _syncLocker = new object();
        string space = " ";
        public RedisCodeOperator(IOCPClient client, RedisCoder redisCoder)
        {
            _client = client;
            _redisCoder = redisCoder;
        }

        public ResponseData Do(RequestType type)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, string.Format("{0}", type.ToString()));
                _client.Send(cmd);
                return _redisCoder.Decoder();
            }
        }

        public ResponseData Do(RequestType type, string key)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key);
                _client.Send(cmd);
                return _redisCoder.Decoder();
            }
        }

        public ResponseData Do(RequestType type, string key, string value)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
                _client.Send(cmd);
                return _redisCoder.Decoder();
            }
        }

        public void DoExpire(string key, int seconds)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(RequestType.EXPIRE, RequestType.EXPIRE.ToString(), key, seconds.ToString());
                _client.Send(cmd);
                _redisCoder.Decoder();
            }
        }

        public void DoExpire(RequestType type, string key, string value, int seconds)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), key, value);
                _client.Send(cmd);
                _redisCoder.Decoder();

                cmd = _redisCoder.Coder(RequestType.EXPIRE, string.Format("{0} {1} {2}", type.ToString(), key, seconds));
                _client.Send(cmd);
                _redisCoder.Decoder();
            }
        }

        public ResponseData Do(RequestType type, string id, string key, string value)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), id, key, value);
                _client.Send(cmd);
                return _redisCoder.Decoder();
            }
        }
        public ResponseData Do(RequestType type, string id, double begin = 0, double end = -1)
        {
            lock (_syncLocker)
            {
                var cmd = _redisCoder.Coder(type, type.ToString(), id, begin.ToString(), end.ToString(), " WITHSCORES");
                _client.Send(cmd);
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
                _client.Send(cmd);
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
                _client.Send(cmd);
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
                _client.Send(cmd);
                return _redisCoder.Decoder();
            }
        }


    }
}
