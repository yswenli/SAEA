/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisCoder
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Base.Net;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SAEA.RedisSocket.Core
{
    internal class RedisCoder
    {
        public const string SEPARATOR = "===========YSWENLI============";

        RequestType _commandName;

        RedisStream _redisStream = new RedisStream();

        string _sendCommand = string.Empty;

        object _locker = new object();

        int _actionTimeout = 60 * 1000;

        RClient _rclient;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="rclient"></param>
        /// <param name="actionTimeout"></param>
        public RedisCoder(RClient rclient, int actionTimeout = 60)
        {
            _rclient = rclient;
            _actionTimeout = actionTimeout * 1000;
        }

        #region 发送编码

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="cmd"></param>
        void Request(string cmd)
        {
            _rclient.Request(Encoding.UTF8.GetBytes(cmd));
        }

        /// <summary>
        /// redis client编码
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="params"></param>
        /// <returns></returns>
        public void CoderByParams(RequestType commandName, params string[] @params)
        {
            @params.NotNull();

            _commandName = commandName;

            var sb = new StringBuilder();

            sb.AppendLine(ConstHelper.ASTERRISK + @params.Length);

            foreach (var param in @params)
            {
                var length = Encoding.UTF8.GetBytes(param).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(param);
            }
            _sendCommand = sb.ToString();

            Request(_sendCommand);
        }

        /// <summary>
        /// redis client编码
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="cmdType"></param>
        /// <param name="params"></param>
        /// <returns></returns>

        public void Coder(RequestType commandName, string cmdType, params string[] @params)
        {
            @params.NotNull();
            _commandName = commandName;
            var sb = new StringBuilder();
            sb.AppendLine(ConstHelper.ASTERRISK + (@params.Length + 1));
            sb.AppendLine(ConstHelper.DOLLAR + cmdType.Length);
            sb.AppendLine(cmdType);
            foreach (var param in @params)
            {
                var length = Encoding.UTF8.GetBytes(param).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(param);
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void CoderForList(RequestType commandName, string id, List<string> list)
        {
            list.NotNull();

            _commandName = commandName;

            var sb = new StringBuilder();
            sb.AppendLine(ConstHelper.ASTERRISK + (list.Count + 2));

            var type = commandName.ToString();
            sb.AppendLine(ConstHelper.DOLLAR + type.Length);
            sb.AppendLine(type);

            var length = Encoding.UTF8.GetBytes(id).Length;
            sb.AppendLine(ConstHelper.DOLLAR + length);
            sb.AppendLine(id);

            foreach (var item in list)
            {
                var len = Encoding.UTF8.GetBytes(item).Length;
                sb.AppendLine(ConstHelper.DOLLAR + len);
                sb.AppendLine(item);
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void CoderForDic(RequestType commandName, Dictionary<string, string> dic)
        {
            dic.NotNull();
            _commandName = commandName;
            var sb = new StringBuilder();
            sb.AppendLine(ConstHelper.ASTERRISK + (dic.Count * 2 + 1));
            var type = commandName.ToString();
            sb.AppendLine(ConstHelper.DOLLAR + type.Length);
            sb.AppendLine(type);
            foreach (var item in dic)
            {
                var length = Encoding.UTF8.GetBytes(item.Key.ToString()).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(item.Key.ToString());

                length = Encoding.UTF8.GetBytes(item.Value.ToString()).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(item.Value.ToString());
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void CoderForDicWidthID(RequestType commandName, string id, Dictionary<string, string> dic)
        {
            dic.NotNull();
            _commandName = commandName;
            var sb = new StringBuilder();
            sb.AppendLine(ConstHelper.ASTERRISK + (dic.Count * 2 + 2));

            var type = commandName.ToString();
            sb.AppendLine(ConstHelper.DOLLAR + type.Length);
            sb.AppendLine(type);

            var length = Encoding.UTF8.GetBytes(id).Length;
            sb.AppendLine(ConstHelper.DOLLAR + length);
            sb.AppendLine(id);
            foreach (var item in dic)
            {
                length = Encoding.UTF8.GetBytes(item.Key.ToString()).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(item.Key.ToString());

                length = Encoding.UTF8.GetBytes(item.Value.ToString()).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(item.Value.ToString());
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void CoderForDicWidthID(RequestType commandName, string id, Dictionary<double, string> dic)
        {
            dic.NotNull();
            _commandName = commandName;
            var sb = new StringBuilder();
            sb.AppendLine(ConstHelper.ASTERRISK + (dic.Count * 2 + 2));

            var type = commandName.ToString();
            sb.AppendLine(ConstHelper.DOLLAR + type.Length);
            sb.AppendLine(type);

            var length = Encoding.UTF8.GetBytes(id).Length;
            sb.AppendLine(ConstHelper.DOLLAR + length);
            sb.AppendLine(id);
            foreach (var item in dic)
            {
                length = Encoding.UTF8.GetBytes(item.Key.ToString()).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(item.Key.ToString());

                length = Encoding.UTF8.GetBytes(item.Value.ToString()).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(item.Value.ToString());
            }
            _sendCommand = sb.ToString();
            Request(_sendCommand);
        }

        public void CoderForRandByScore(RequestType commandName, string key, double min, double max, RangType rangType, long offset, int count, bool withScore = false)
        {
            _commandName = commandName;

            var sb = new StringBuilder();

            if (withScore)
            {
                if (offset > -1)
                {
                    sb.AppendLine(ConstHelper.ASTERRISK + 8);
                }
                else
                {
                    sb.AppendLine(ConstHelper.ASTERRISK + 5);
                }
            }
            else
            {
                if (offset > -1)
                {
                    sb.AppendLine(ConstHelper.ASTERRISK + 7);
                }
                else
                {
                    sb.AppendLine(ConstHelper.ASTERRISK + 4);
                }
            }

            var type = commandName.ToString();
            sb.AppendLine(ConstHelper.DOLLAR + type.Length);
            sb.AppendLine(type);

            var length = Encoding.UTF8.GetBytes(key).Length;
            sb.AppendLine(ConstHelper.DOLLAR + length);
            sb.AppendLine(key);

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
            sb.AppendLine(ConstHelper.DOLLAR + length);
            sb.AppendLine(minStr);

            length = Encoding.UTF8.GetBytes(maxStr).Length;
            sb.AppendLine(ConstHelper.DOLLAR + length);
            sb.AppendLine(maxStr);

            if (withScore)
            {
                sb.AppendLine(ConstHelper.DOLLAR + 10);
                sb.AppendLine("WITHSCORES");
            }

            if (offset > -1)
            {
                sb.AppendLine(ConstHelper.DOLLAR + 5);
                sb.AppendLine("LIMIT");

                var offsetStr = offset.ToString();
                length = Encoding.UTF8.GetBytes(offsetStr).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(offsetStr);

                var countStr = count.ToString();
                length = Encoding.UTF8.GetBytes(countStr).Length;
                sb.AppendLine(ConstHelper.DOLLAR + length);
                sb.AppendLine(countStr);
            }

            _sendCommand = sb.ToString();
            Request(_sendCommand);
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
        /// <param name="timeOut">设置收取消息超时时间，默认30秒</param>
        /// <returns></returns>
        string GetRedisReply()
        {
            string str = string.Empty;

            bool loop = false;

            int timeCount = 0;

            do
            {
                timeCount++;

                str = _redisStream.ReadLine();

                loop = string.IsNullOrEmpty(str);

                if (loop)
                {
                    Thread.Sleep(1);

                    if (timeCount >= _actionTimeout) throw new TimeoutException("-Err:Operation is timeout!");
                }
            }
            while (loop);

            return str;
        }

        public bool IsSubed = false;


        /// <summary>
        /// 读取剩余的内容
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="len"></param>
        /// <param name="addSeparator"></param>
        /// <returns></returns>
        private StringBuilder GetLastSB(StringBuilder sb, int len, bool addSeparator = false)
        {
            string str = string.Empty;

            bool loop = false;

            int timeCount = 0;

            if (len > 0)
            {
                do
                {
                    timeCount++;

                    str = _redisStream.ReadBlock(len);

                    loop = string.IsNullOrEmpty(str);

                    if (loop)
                    {
                        Thread.Sleep(1);
                        if (timeCount >= _actionTimeout) throw new TimeoutException("-Err:Operation is timeout!");
                    }
                }
                while (loop);

                sb.Append(str);
            }

            if (addSeparator)
                sb.Append(SEPARATOR);

            return sb;
        }

        /// <summary>
        /// 解析从redis返回的命令
        /// </summary>
        /// <returns></returns>
        public ResponseData Decoder()
        {
            string command = string.Empty;

            string error = string.Empty;

            var len = 0;

            command = GetRedisReply();

            var responseData = new ResponseData();

            if (command.IndexOf("-") == 0)
            {
                responseData.Type = ResponseType.Error;
                responseData.Data = command;
                return responseData;
            }

            while (command == ConstHelper.ENTER)
            {
                command = GetRedisReply();

                if (command.IndexOf("-") == 0)
                {
                    responseData.Type = ResponseType.Error;
                    responseData.Data = command;
                    return responseData;
                }
            }

            var temp = Redirect(command);

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

                switch (_commandName)
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
                    case RequestType.HDEL:
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
                            responseData.Data += GetLastSB(new StringBuilder(), len).ToString();
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
                        if (rn > 0)
                        {
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReply(), out error);
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
                                sb = GetLastSB(sb, len, true);
                            }
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

                            responseData.Data += GetLastSB(new StringBuilder(), len).ToString();
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
                        if (rn > 0)
                        {
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReply(), out error);
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
                                sb = GetLastSB(sb, len, true);
                            }
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
                        len = GetWordsNum(GetRedisReply(), out error);
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
                        var ssb = GetLastSB(new StringBuilder(), len, false);
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
                        ssb = GetLastSB(new StringBuilder(), len, false);
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
                        ssb = GetLastSB(new StringBuilder(), len, true);
                        responseData.Type = ResponseType.String;
                        responseData.Data = ssb.ToString();
                        break;
                    case RequestType.SUBSCRIBE:
                        var r = string.Empty;
                        while (IsSubed)
                        {
                            r = GetRedisReply();
                            if (string.Compare(r, "message\r\n", true) == 0)
                            {
                                responseData.Type = ResponseType.Sub;
                                GetRedisReply();
                                responseData.Data = GetRedisReply();
                                GetRedisReply();
                                responseData.Data += GetRedisReply();
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
                        var wNum = GetWordsNum(GetRedisReply(), out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            responseData.Type = ResponseType.Error;
                            responseData.Data = error;
                            return responseData;
                        }
                        GetRedisReply();
                        wNum = GetWordsNum(GetRedisReply(), out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            responseData.Type = ResponseType.Error;
                            responseData.Data = error;
                            return responseData;
                        }
                        var channel = GetRedisReply();
                        var vNum = GetValue(GetRedisReply(), out error);
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
                            command = GetRedisReply();
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
                        while (rn <= 0)
                        {
                            command = GetRedisReply();
                            rn = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                        }
                        //
                        len = GetWordsNum(GetRedisReply(), out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            responseData.Type = ResponseType.Error;
                            responseData.Data = error;
                            return responseData;
                        }
                        int.TryParse(GetRedisReply(), out offset);
                        sb.Append("offset:" + offset + SEPARATOR);
                        //
                        command = GetRedisReply();
                        while (command == ConstHelper.ENTER)
                        {
                            command = GetRedisReply();
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
                            command = GetRedisReply();
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
                            len = GetWordsNum(GetRedisReply(), out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            if (len >= 0)
                            {
                                sb = GetLastSB(sb, len, true);
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
                        for (int i = 0; i < rn; i++)
                        {
                            command = GetRedisReply();
                            var srows = GetRowNum(command, out error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                responseData.Type = ResponseType.Error;
                                responseData.Data = error;
                                return responseData;
                            }
                            for (int j = 0; j < srows; j++)
                            {
                                command = GetRedisReply();
                                len = GetWordsNum(command, out error);
                                if (len >= 0)
                                {
                                    sb = GetLastSB(sb, len, true);
                                }
                                else
                                {
                                    var ssrows = GetRowNum(command, out error);

                                    for (int k = 0; k < ssrows; k++)
                                    {
                                        command = GetRedisReply();
                                        len = GetWordsNum(command, out error);
                                        sb = GetLastSB(sb, len, true);
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
                        responseData.Data = GetLastSB(new StringBuilder(), len).ToString();
                        break;
                    default:
                        responseData.Type = ResponseType.Undefined;
                        responseData.Data = "未知的命令，请自行添加解码规则！";
                        break;
                }
            }
            return responseData;
        }

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

        private static ResponseData Redirect(string command)
        {
            ResponseData result = null;
            if (command.Contains("-MOVED"))
            {
                result = new ResponseData()
                {
                    Type = ResponseType.Redirect,
                    Data = command.Split(" ")[2].Replace(ConstHelper.ENTER, "")
                };
            }
            return result;
        }

        #endregion



        public void Dispose()
        {
            _redisStream.Dispose();
        }
    }
}
