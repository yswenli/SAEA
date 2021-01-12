/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisCoder
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
using SAEA.RedisSocket.Base.Net;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis coder/decoder
    /// </summary>
    internal partial class RedisCoder : IDisposable
    {
        public const string SEPARATOR = "===========YSWENLI============";

        public const string MOVED = "-MOVED";

        RedisStream _redisStream = new RedisStream();

        string _sendCommand = string.Empty;

        RClient _rclient;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rclient"></param>
        public RedisCoder(RClient rclient)
        {
            _rclient = rclient;
        }

        #region 发送编码

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="cmd"></param>
        public void Request(string cmd)
        {
            _rclient.Request(Encoding.UTF8.GetBytes(cmd));
        }

        /// <summary>
        /// redis client编码
        /// </summary>
        /// <param name="requestType"></param>
        /// <returns></returns>
        public string CodeOnlyParams(RequestType requestType)
        {
            return CodeOnlyParams(requestType);
        }

        /// <summary>
        /// redis client编码
        /// </summary>
        /// <param name="params"></param>
        /// <returns></returns>
        public string CodeOnlyParams(params string[] @params)
        {
            @params.NotNull();

            var sb = new StringBuilder();

            sb.Append(ConstHelper.ASTERRISK + @params.Length + ConstHelper.ENTER);

            foreach (var param in @params)
            {
                var length = Encoding.UTF8.GetBytes(param).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(param + ConstHelper.ENTER);
            }
            return sb.ToString();
        }

        /// <summary>
        /// RequestOnlyParams
        /// </summary>
        /// <param name="params"></param>
        public void RequestOnlyParams(params string[] @params)
        {
            Request(CodeOnlyParams(@params));
        }

        /// <summary>
        /// redis client编码
        /// </summary>
        /// <param name="requestType"></param>
        /// <param name="params"></param>
        /// <returns></returns>

        public void Request(RequestType requestType, params string[] @params)
        {
            Request(Coder(requestType, @params));
        }

        /// <summary>
        /// redis client编码
        /// </summary>
        /// <param name="requestType"></param>
        /// <param name="params"></param>
        /// <returns></returns>
        public string Coder(RequestType requestType, params string[] @params)
        {
            @params.NotNull();
            var type = requestType.ToString();
            var sb = new StringBuilder();
            sb.Append(ConstHelper.ASTERRISK + (@params.Length + 1) + ConstHelper.ENTER);
            sb.Append(ConstHelper.DOLLAR + type.Length + ConstHelper.ENTER);
            sb.Append(type + ConstHelper.ENTER);
            foreach (var param in @params)
            {
                var length = Encoding.UTF8.GetBytes(param).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(param + ConstHelper.ENTER);
            }
            return sb.ToString();
        }

        public void RequestForList(RequestType requestType, string id, IEnumerable<string> list)
        {
            list.NotNull();

            var sb = new StringBuilder();
            sb.Append(ConstHelper.ASTERRISK + (list.Count() + 2) + ConstHelper.ENTER);

            var type = requestType.ToString();
            sb.Append(ConstHelper.DOLLAR + type.Length + ConstHelper.ENTER);
            sb.Append(type + ConstHelper.ENTER);

            var length = Encoding.UTF8.GetBytes(id).Length;
            sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
            sb.Append(id + ConstHelper.ENTER);

            foreach (var item in list)
            {
                var len = Encoding.UTF8.GetBytes(item).Length;
                sb.Append(ConstHelper.DOLLAR + len + ConstHelper.ENTER);
                sb.Append(item + ConstHelper.ENTER);
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void RequestForDic(RequestType requestType, Dictionary<string, string> dic)
        {
            dic.NotNull();
            var sb = new StringBuilder();
            sb.Append(ConstHelper.ASTERRISK + (dic.Count * 2 + 1) + ConstHelper.ENTER);
            var type = requestType.ToString();
            sb.Append(ConstHelper.DOLLAR + type.Length + ConstHelper.ENTER);
            sb.Append(type + ConstHelper.ENTER);
            foreach (var item in dic)
            {
                var length = Encoding.UTF8.GetBytes(item.Key).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(item.Key + ConstHelper.ENTER);

                length = Encoding.UTF8.GetBytes(item.Value).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(item.Value + ConstHelper.ENTER);
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void RequestForDicWidthID(RequestType requestType, string id, Dictionary<string, string> dic)
        {
            dic.NotNull();
            var sb = new StringBuilder();
            sb.Append(ConstHelper.ASTERRISK + (dic.Count * 2 + 2) + ConstHelper.ENTER);

            var type = requestType.ToString();
            sb.Append(ConstHelper.DOLLAR + type.Length + ConstHelper.ENTER);
            sb.Append(type + ConstHelper.ENTER);

            var length = Encoding.UTF8.GetBytes(id).Length;
            sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
            sb.Append(id + ConstHelper.ENTER);
            foreach (var item in dic)
            {
                length = Encoding.UTF8.GetBytes(item.Key.ToString()).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(item.Key + ConstHelper.ENTER);

                length = Encoding.UTF8.GetBytes(item.Value).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(item.Value + ConstHelper.ENTER);
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void RequestForDicWidthID(RequestType requestType, string id, Dictionary<double, string> dic)
        {
            Request(CodeForDicWidthID(requestType, id, dic));
        }

        public string CodeForDicWidthID(RequestType requestType, string id, Dictionary<double, string> dic)
        {
            dic.NotNull();
            var sb = new StringBuilder();
            sb.Append(ConstHelper.ASTERRISK + (dic.Count * 2 + 2) + ConstHelper.ENTER);

            var type = requestType.ToString();
            sb.Append(ConstHelper.DOLLAR + type.Length + ConstHelper.ENTER);
            sb.Append(type + ConstHelper.ENTER);

            var length = Encoding.UTF8.GetBytes(id).Length;
            sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
            sb.Append(id + ConstHelper.ENTER);
            foreach (var item in dic)
            {
                length = Encoding.UTF8.GetBytes(item.Key.ToString()).Length;
                sb.Append(ConstHelper.DOLLAR + length);
                sb.Append(item.Key.ToString());

                length = Encoding.UTF8.GetBytes(item.Value).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(item.Value + ConstHelper.ENTER);
            }
            return sb.ToString();
        }

        public string CodeForRandByScore(RequestType requestType, string key, double min, double max, RangType rangType, long offset, int count, bool withScore = false)
        {
            var sb = new StringBuilder();

            if (withScore)
            {
                if (offset > -1)
                {
                    sb.Append(ConstHelper.ASTERRISK + 8 + ConstHelper.ENTER);
                }
                else
                {
                    sb.Append(ConstHelper.ASTERRISK + 5 + ConstHelper.ENTER);
                }
            }
            else
            {
                if (offset > -1)
                {
                    sb.Append(ConstHelper.ASTERRISK + 7 + ConstHelper.ENTER);
                }
                else
                {
                    sb.Append(ConstHelper.ASTERRISK + 4 + ConstHelper.ENTER);
                }
            }

            var type = requestType.ToString();
            sb.Append(ConstHelper.DOLLAR + type.Length + ConstHelper.ENTER);
            sb.Append(type + ConstHelper.ENTER);

            var length = Encoding.UTF8.GetBytes(key).Length;
            sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
            sb.Append(key + ConstHelper.ENTER);

            var minStr = string.Empty;
            var maxStr = string.Empty;

            switch (rangType)
            {
                case RangType.IncludeLeft:
                    minStr = min.ToString();
                    maxStr = $"({max}";
                    break;
                case RangType.InCludeRight:
                    minStr = $"({min}";
                    maxStr = max.ToString();
                    break;
                case RangType.Both:
                    minStr = $"({min}";
                    maxStr = $"({max}";
                    break;
                default:
                    minStr = min.ToString();
                    maxStr = max.ToString();
                    break;
            }

            length = Encoding.UTF8.GetBytes(minStr).Length;
            sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
            sb.Append(minStr + ConstHelper.ENTER);

            length = Encoding.UTF8.GetBytes(maxStr).Length;
            sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
            sb.Append(maxStr + ConstHelper.ENTER);

            if (withScore)
            {
                sb.Append(ConstHelper.DOLLAR + 10 + ConstHelper.ENTER);
                sb.Append("WITHSCORES" + ConstHelper.ENTER);
            }

            if (offset > -1)
            {
                sb.Append(ConstHelper.DOLLAR + 5 + ConstHelper.ENTER);
                sb.Append("LIMIT" + ConstHelper.ENTER);

                var offsetStr = offset.ToString();
                length = Encoding.UTF8.GetBytes(offsetStr).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(offsetStr + ConstHelper.ENTER);

                var countStr = count.ToString();
                length = Encoding.UTF8.GetBytes(countStr).Length;
                sb.Append(ConstHelper.DOLLAR + length + ConstHelper.ENTER);
                sb.Append(countStr + ConstHelper.ENTER);
            }

            return sb.ToString();

        }


        public void RequestForRandByScore(RequestType requestType, string key, double min, double max, RangType rangType, long offset, int count, bool withScore = false)
        {
            Request(CodeForRandByScore(requestType, key, min, max, RangType.None, offset, count, withScore));
        }
        #endregion

        #region 接收解码

        /// <summary>
        /// 接收来自RedisServer的命令
        /// </summary>
        /// <param name="command"></param>
        public void Enqueue(byte[] msg)
        {
            _redisStream.Write(msg);
        }

        /// <summary>
        /// 获取redis回复的内容
        /// </summary>
        /// <param name="ctoken">设置收取消息超时时间，默认30秒</param>
        /// <returns></returns>
        string GetRedisReplyLine(CancellationToken ctoken)
        {
            try
            {
                string str = string.Empty;
                do
                {
                    str = _redisStream.ReadLine();

                    if (string.IsNullOrEmpty(str))
                    {
                        Thread.Yield();
                    }
                    else
                    {
                        break;
                    }
                }
                while (!ctoken.IsCancellationRequested);

                return str;
            }
            catch (Exception ex)
            {
                return "-Err:GetRedisReply Timeout," + ex.Message;
            }
        }

        public bool IsSubed = false;


        /// <summary>
        /// 读取剩余的内容
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="len"></param>
        /// <param name="ctoken"></param>
        /// <param name="addSeparator"></param>
        /// <returns></returns>
        private StringBuilder GetRedisReplyBlob(StringBuilder sb, int len, CancellationToken ctoken, bool addSeparator = false)
        {
            sb.Append(_redisStream.ReadBlock(len, ctoken));

            if (addSeparator)
                sb.Append(SEPARATOR);

            return sb;
        }

        /// <summary>
        /// 解析从redis返回的命令
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="requestType"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public ResponseData<T> Decoder<T>(RequestType requestType, CancellationToken ctoken)
        {
            var responseData = new ResponseData<T>();

            try
            {
                string command = string.Empty;

                string error = string.Empty;

                var len = 0;

                command = GetRedisReplyLine(ctoken);

                if (string.IsNullOrEmpty(command)) return null;

                if (command.IndexOf("-") == 0 && command.IndexOf(MOVED) == -1)
                {
                    responseData.Type = ResponseType.Error;
                    responseData.Data = command;
                    return responseData;
                }

                while (command == ConstHelper.ENTER)
                {
                    command = GetRedisReplyLine(ctoken);

                    if (command.IndexOf("-") == 0 && command.IndexOf(MOVED) == -1)
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = command;
                        return responseData;
                    }
                    if (string.IsNullOrEmpty(command)) return null;
                }

                var temp = Redirect<T>(command);

                if (temp != null)
                {
                    responseData = temp;
                }
                else
                {
                    if (command.IndexOf("-") == 0)
                    {
                        responseData.Type = ResponseType.Error;
                        responseData.Data = command;
                        return responseData;
                    }

                    switch (requestType)
                    {
                        case RequestType.PING:
                            if (GetStatus(command, out error))
                            {
                                responseData.Type = ResponseType.OK;
                                responseData.Data = "PONG";
                            }
                            else
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                            }
                            break;
                        case RequestType.AUTH:
                        case RequestType.FLUSHALL:
                        case RequestType.SELECT:
                        case RequestType.SLAVEOF:
                        case RequestType.SET:
                        case RequestType.MSET:
                        case RequestType.MSETNX:
                        case RequestType.DEL:
                        case RequestType.HSET:
                        case RequestType.HMSET:
                        case RequestType.LSET:
                        case RequestType.LTRIM:
                        case RequestType.RENAME:
                        case RequestType.CLUSTER_MEET:
                        case RequestType.CLUSTER_FORGET:
                        case RequestType.CLUSTER_REPLICATE:
                        case RequestType.CLUSTER_SAVECONFIG:
                        case RequestType.CLUSTER_ADDSLOTS:
                        case RequestType.CLUSTER_DELSLOTS:
                        case RequestType.CLUSTER_FLUSHSLOTS:
                        case RequestType.CLUSTER_SETSLOT:
                        case RequestType.CONFIG_SET:
                        case RequestType.XGROUP:
                            if (GetStatus(command, out error))
                            {
                                responseData.Type = ResponseType.OK;
                                responseData.Data = "OK";
                            }
                            else
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                            }
                            break;
                        case RequestType.TYPE:
                            if (GetStatusString(command, out string msg))
                            {
                                responseData.Type = ResponseType.OK;
                            }
                            else
                            {
                                responseData.Type = ResponseType.Error;
                            }
                            responseData.Data = msg;
                            break;
                        case RequestType.GET:
                        case RequestType.GETSET:
                        case RequestType.HGET:
                        case RequestType.LPOP:
                        case RequestType.RPOP:
                        case RequestType.SRANDMEMBER:
                        case RequestType.SPOP:
                        case RequestType.RANDOMKEY:
                        case RequestType.XADD:
                            len = GetWordsNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                            }
                            else if (len == -1)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = string.Empty;
                            }
                            else
                            {
                                responseData.Type = ResponseType.String;
                                responseData.Data += GetRedisReplyBlob(new StringBuilder(), len, ctoken).ToString();
                            }
                            break;
                        case RequestType.KEYS:
                        case RequestType.MGET:
                        case RequestType.HKEYS:
                        case RequestType.HVALS:
                        case RequestType.HMGET:
                        case RequestType.LRANGE:
                        case RequestType.BLPOP:
                        case RequestType.BRPOP:
                        case RequestType.SMEMBERS:
                        case RequestType.SINTER:
                        case RequestType.SUNION:
                        case RequestType.SDIFF:
                        case RequestType.ZRANGEBYLEX:
                        case RequestType.CLUSTER_GETKEYSINSLOT:
                        case RequestType.CONFIG_GET:
                            var sb = new StringBuilder();
                            var rn = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            if (rn <= 0)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = "";
                                return responseData;
                            }
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    responseData.Type = ResponseType.Error;
                                    responseData.Data = error;
                                    return responseData;
                                }
                                if (len == -1)
                                {
                                    responseData.Type = ResponseType.Empty;
                                    responseData.Data = string.Empty;
                                    return responseData;
                                }
                                sb = GetRedisReplyBlob(sb, len, ctoken, true);
                            }
                            responseData.Type = ResponseType.Lines;
                            responseData.Data = sb.ToString();
                            break;
                        case RequestType.BRPOPLPUSH:
                            responseData.Type = ResponseType.Value;
                            len = GetWordsNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                            }
                            else if (len == -1)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = string.Empty;
                            }
                            else
                            {
                                responseData.Type = ResponseType.String;

                                responseData.Data += GetRedisReplyBlob(new StringBuilder(), len, ctoken).ToString();
                            }
                            break;
                        case RequestType.HGETALL:
                        case RequestType.ZRANGE:
                        case RequestType.ZREVRANGE:
                        case RequestType.ZRANGEBYSCORE:
                        case RequestType.ZREVRANGEBYSCORE:
                            responseData.Type = ResponseType.KeyValues;
                            sb = new StringBuilder();
                            rn = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            if (rn <= 0)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = "";
                                return responseData;
                            }
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    responseData.Type = ResponseType.Error;
                                    responseData.Data = error;
                                    return responseData;
                                }
                                if (len == -1)
                                {
                                    responseData.Type = ResponseType.Empty;
                                    responseData.Data = string.Empty;
                                    return responseData;
                                }
                                sb = GetRedisReplyBlob(sb, len, ctoken, true);
                            }
                            responseData.Data = sb.ToString();
                            break;
                        case RequestType.DBSIZE:
                        case RequestType.FLUSHDB:
                        case RequestType.STRLEN:
                        case RequestType.APPEND:
                        case RequestType.TTL:
                        case RequestType.PTTL:
                        case RequestType.EXISTS:
                        case RequestType.EXPIRE:
                        case RequestType.EXPIREAT:
                        case RequestType.PERSIST:
                        case RequestType.SETNX:
                        case RequestType.HEXISTS:
                        case RequestType.HLEN:
                        case RequestType.HDEL:
                        case RequestType.HSTRLEN:
                        case RequestType.HINCRBY:
                        case RequestType.LLEN:
                        case RequestType.INCR:
                        case RequestType.INCRBY:
                        case RequestType.DECR:
                        case RequestType.DECRBY:
                        case RequestType.LPUSH:
                        case RequestType.LPUSHX:
                        case RequestType.RPUSH:
                        case RequestType.RPUSHX:
                        case RequestType.RPOPLPUSH:
                        case RequestType.LINSERT:
                        case RequestType.SADD:
                        case RequestType.SCARD:
                        case RequestType.SISMEMBER:
                        case RequestType.SREM:
                        case RequestType.SMOVE:
                        case RequestType.SINTERSTORE:
                        case RequestType.SUNIONSTORE:
                        case RequestType.LREM:
                        case RequestType.SDIFFSTORE:
                        case RequestType.ZADD:
                        case RequestType.ZSCORE:
                        case RequestType.ZINCRBY:
                        case RequestType.ZCARD:
                        case RequestType.ZCOUNT:
                        case RequestType.ZRANK:
                        case RequestType.ZREVRANK:
                        case RequestType.ZREM:
                        case RequestType.ZREMRANGEBYRANK:
                        case RequestType.ZREMRANGEBYSCORE:
                        case RequestType.ZLEXCOUNT:
                        case RequestType.ZREMRANGEBYLEX:
                        case RequestType.PUBLISH:
                        case RequestType.CLUSTER_KEYSLOT:
                        case RequestType.CLUSTER_COUNTKEYSINSLOT:
                        case RequestType.GEOADD:
                            var val = GetValue(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            responseData.Type = ResponseType.Value;
                            responseData.Data = val.ToString();
                            break;

                        case RequestType.LINDEX:
                            val = GetValue(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            len = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                            if (len == -1)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = string.Empty;
                                return responseData;
                            }
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            var ssb = GetRedisReplyBlob(new StringBuilder(), len, ctoken, false);
                            responseData.Type = ResponseType.String;
                            responseData.Data = ssb.ToString();

                            break;
                        case RequestType.INFO:
                        case RequestType.INCRBYFLOAT:
                        case RequestType.HINCRBYFLOAT:
                        case RequestType.CLUSTER_INFO:
                        case RequestType.CLUSTER_NODES:
                            len = GetWordsNum(command, out error);
                            if (len == -1)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = string.Empty;
                                return responseData;
                            }
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            ssb = GetRedisReplyBlob(new StringBuilder(), len, ctoken, false);
                            responseData.Type = ResponseType.String;
                            responseData.Data = ssb.ToString();
                            break;
                        case RequestType.CLIENT_LIST:
                            len = GetWordsNum(command, out error);
                            if (len == -1)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = string.Empty;
                                return responseData;
                            }
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            ssb = GetRedisReplyBlob(new StringBuilder(), len, ctoken, true);
                            responseData.Type = ResponseType.String;
                            responseData.Data = ssb.ToString();
                            break;
                        case RequestType.SUBSCRIBE:
                            var r = string.Empty;
                            while (IsSubed)
                            {
                                r = GetRedisReplyLine(ctoken);
                                if (string.Compare(r, "message\r\n", true) == 0)
                                {
                                    responseData.Type = ResponseType.Sub;
                                    GetRedisReplyLine(ctoken);
                                    responseData.Data = GetRedisReplyLine(ctoken);
                                    GetRedisReplyLine(ctoken);
                                    responseData.Data += GetRedisReplyLine(ctoken);
                                    break;
                                }
                            }
                            break;
                        case RequestType.UNSUBSCRIBE:
                            var rNum = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            if (rNum <= 0)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = "";
                                return responseData;
                            }
                            var wNum = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            GetRedisReplyLine(ctoken);
                            wNum = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            var channel = GetRedisReplyLine(ctoken);
                            var vNum = GetValue(GetRedisReplyLine(ctoken), out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            IsSubed = false;
                            break;
                        case RequestType.SCAN:
                        case RequestType.HSCAN:
                        case RequestType.SSCAN:
                        case RequestType.ZSCAN:
                            responseData.Type = ResponseType.Lines;
                            while (command == ConstHelper.ENTER)
                            {
                                command = GetRedisReplyLine(ctoken);
                            }
                            sb = new StringBuilder();
                            int offset = 0;
                            rn = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            if (rn <= 0)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = "";
                                return responseData;
                            }
                            while (rn <= 0)
                            {
                                command = GetRedisReplyLine(ctoken);
                                rn = GetRowNum(command, out error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    responseData.Type = ResponseType.Error;
                                    responseData.Data = error;
                                    return responseData;
                                }
                            }
                            //
                            len = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            int.TryParse(GetRedisReplyLine(ctoken), out offset);
                            sb.Append("offset:" + offset + SEPARATOR);
                            //
                            command = GetRedisReplyLine(ctoken);
                            while (command == ConstHelper.ENTER)
                            {
                                command = GetRedisReplyLine(ctoken);
                            }
                            if (string.IsNullOrEmpty(command))
                            {
                                break;
                            }
                            rn = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            while (rn < 0)
                            {
                                command = GetRedisReplyLine(ctoken);
                                rn = GetRowNum(command, out error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    responseData.Type = ResponseType.Error;
                                    responseData.Data = error;
                                    return responseData;
                                }
                            }
                            //
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReplyLine(ctoken), out error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    responseData.Type = ResponseType.Error;
                                    responseData.Data = error;
                                    return responseData;
                                }
                                if (len >= 0)
                                {
                                    sb = GetRedisReplyBlob(sb, len, ctoken, true);
                                }
                            }
                            responseData.Data = sb.ToString();
                            break;
                        case RequestType.GEOPOS:
                        case RequestType.GEORADIUS:
                        case RequestType.GEORADIUSBYMEMBER:
                            responseData.Type = ResponseType.Lines;
                            sb = new StringBuilder();
                            rn = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            if (rn <= 0)
                            {
                                responseData.Type = ResponseType.Empty;
                                responseData.Data = "";
                                return responseData;
                            }
                            for (int i = 0; i < rn; i++)
                            {
                                command = GetRedisReplyLine(ctoken);
                                var srows = GetRowNum(command, out error);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    responseData.Type = ResponseType.Error;
                                    responseData.Data = error;
                                    return responseData;
                                }
                                for (int j = 0; j < srows; j++)
                                {
                                    command = GetRedisReplyLine(ctoken);
                                    len = GetWordsNum(command, out error);
                                    if (len >= 0)
                                    {
                                        sb = GetRedisReplyBlob(sb, len, ctoken, true);
                                    }
                                    else
                                    {
                                        var ssrows = GetRowNum(command, out error);

                                        for (int k = 0; k < ssrows; k++)
                                        {
                                            command = GetRedisReplyLine(ctoken);
                                            len = GetWordsNum(command, out error);
                                            sb = GetRedisReplyBlob(sb, len, ctoken, true);
                                        }
                                    }
                                }
                            }
                            responseData.Data = sb.ToString();
                            break;
                        case RequestType.GEODIST:
                            len = GetWordsNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                break;
                            }
                            responseData.Type = ResponseType.Value;
                            responseData.Data = GetRedisReplyBlob(new StringBuilder(), len, ctoken).ToString();
                            break;
                        default:
                            responseData.Type = ResponseType.Undefined;
                            responseData.Data = "未知的命令，请自行添加解码规则！";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("RedisCoder.Decoder", ex);
                responseData.Type = ResponseType.Error;
                responseData.Data = "操作超时";
            }
            return responseData;
        }
        #region 解析

        private static bool GetStatus(string command, out string error)
        {
            error = string.Empty;
            var result = false;
            if (!string.IsNullOrEmpty(command) && command.Length > 0)
            {
                if (command.IndexOf("+") == 0)
                {
                    result = true;
                }
                else
                {
                    error = StringHelper.Substring(command, 1);
                    result = false;
                }
            }
            return result;
        }
        private static bool GetStatusString(string command, out string msg)
        {
            msg = string.Empty;
            var result = false;
            if (!string.IsNullOrEmpty(command) && command.Length > 0)
            {
                if (command.IndexOf("+") == 0)
                {
                    result = true;
                }
                else
                {
                    result = false;
                }
                msg = StringHelper.Substring(command, 1, command.Length - 3);
            }
            return result;
        }

        private static int GetRowNum(string command, out string error)
        {
            error = "";
            int num = -1;

            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.IndexOf(ConstHelper.ASTERRISK) == 0)
                {
                    num = command.ParseToInt(1, command.Length - 3);
                }
                if (command.Length > 2 && command.IndexOf("-") == 0)
                {
                    error = StringHelper.Substring(command, 1);
                }
            }
            return num;
        }

        private static int GetWordsNum(string command, out string error)
        {
            error = "";
            int num = -1;
            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.IndexOf(ConstHelper.DOLLAR) == 0)
                {
                    num = int.Parse(StringHelper.Substring(command, 1));
                }
                if (command.Length > 2 && command.IndexOf("-") == 0)
                {
                    error = StringHelper.Substring(command, 1);
                }
            }

            return num;
        }

        private static string GetValue(string command, out string error)
        {
            error = "";
            string num = "0";
            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.IndexOf(":") == 0)
                {
                    num = StringHelper.Substring(command, 1);
                }
                if (command.Length > 2 && command.IndexOf("-") == 0)
                {
                    error = StringHelper.Substring(command, 1);
                }
            }
            return num;
        }

        private static ResponseData<T> Redirect<T>(string command)
        {
            ResponseData<T> result = null;
            if (command.IndexOf(MOVED) == 0)
            {
                result = new ResponseData<T>()
                {
                    Type = ResponseType.Redirect,
                    Data = command.Split(" ")[2].Replace(ConstHelper.ENTER, "")
                };
            }
            return result;
        }

        
        #endregion



        #endregion



        public void Dispose()
        {
            _redisStream.Dispose();
        }
    }
}
