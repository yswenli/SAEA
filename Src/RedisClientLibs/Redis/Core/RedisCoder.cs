/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RedisCoder
*版本号： V1.0.0.0
*唯一标识：5bb9b438-ce6f-4faa-b786-a0b372aaecf2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 9:38:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 9:38:50
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis 命令编码与解析
    /// </summary>
    public class RedisCoder : IDisposable
    {
        AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        RequestType _commandName;

        ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();

        string _sendCommand = string.Empty;

        readonly string _enter = "\r\n";

        /// <summary>
        /// 常规编码
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="params"></param>
        /// <returns></returns>
        public string Coder(RequestType commandName, params string[] @params)
        {
            _autoResetEvent.WaitOne();
            _commandName = commandName;
            var sb = new StringBuilder();
            sb.AppendLine("*" + @params.Length);
            foreach (var param in @params)
            {
                sb.AppendLine("$" + param.Length);
                sb.AppendLine(param);
            }
            _sendCommand = sb.ToString();
            return _sendCommand;
        }

        /// <summary>
        /// 接收来自RedisServer的命令
        /// </summary>
        /// <param name="command"></param>
        public void Enqueue(string command)
        {
            _queue.Enqueue(command);
        }

        /// <summary>
        /// 获取redis回复的内容
        /// </summary>
        /// <param name="timeOut">设置收取消息超时时间，默认30秒</param>
        /// <returns></returns>
        public string GetRedisReply(int timeOut = 30 * 1000)
        {
            bool stopped = false;
            var task = Task.Factory.StartNew(() => BlockDequeue(stopped));
            if (!Task.WaitAll(new Task[] { task }, timeOut))
            {
                throw new Exception("redis server reply time out!");
            }
            stopped = true;
            return task.Result;
        }

        /// <summary>
        /// 从收到的消息本地队列中出队
        /// </summary>
        /// <returns></returns>
        private string BlockDequeue(bool stopped = false)
        {
            var result = string.Empty;
            do
            {
                if (!_queue.TryDequeue(out result))
                {
                    Thread.Sleep(0);
                }
            }
            while (string.IsNullOrEmpty(result) && !stopped);
            return result;
        }

        public bool IsSubed = false;

        /// <summary>
        /// 解析从redis返回的命令
        /// </summary>
        /// <returns></returns>
        public ResponseData Decoder()
        {
            var result = new ResponseData();

            string command = null;

            string error = null;

            var len = 0;

            command = GetRedisReply();

            while (command == _enter)
            {
                command = GetRedisReply();
            }

            var temp = Redirect(command);

            if (temp != null)
            {
                result = temp;
            }
            else
            {
                switch (_commandName)
                {
                    case RequestType.PING:
                        if (GetStatus(command, out error))
                        {
                            result.Type = ResponseType.OK;
                            result.Data = "PONG";
                        }
                        else
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                        }
                        break;
                    case RequestType.AUTH:
                    case RequestType.SELECT:
                    case RequestType.SLAVEOF:
                    case RequestType.SET:
                    case RequestType.DEL:
                    case RequestType.HSET:
                    case RequestType.HDEL:
                    case RequestType.LSET:
                    case RequestType.RENAME:
                    case RequestType.CLUSTER_MEET:
                    case RequestType.CLUSTER_FORGET:
                    case RequestType.CLUSTER_REPLICATE:
                    case RequestType.CLUSTER_SAVECONFIG:
                    case RequestType.CLUSTER_ADDSLOTS:
                    case RequestType.CLUSTER_DELSLOTS:
                    case RequestType.CLUSTER_FLUSHSLOTS:
                    case RequestType.CLUSTER_SETSLOT:
                        if (GetStatus(command, out error))
                        {
                            result.Type = ResponseType.OK;
                            result.Data = "OK";
                        }
                        else
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                        }
                        break;
                    case RequestType.TYPE:
                        if (GetStatusString(command, out string msg))
                        {
                            result.Type = ResponseType.OK;
                        }
                        else
                        {
                            result.Type = ResponseType.Error;
                        }
                        result.Data = msg;
                        break;
                    case RequestType.GET:
                    case RequestType.GETSET:
                    case RequestType.HGET:
                    case RequestType.LPOP:
                    case RequestType.RPOP:
                    case RequestType.SRANDMEMBER:
                    case RequestType.SPOP:
                        len = GetWordsNum(command, out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                        }
                        else if (len == -1)
                        {
                            result.Type = ResponseType.Empty;
                            result.Data = error;
                        }
                        else
                        {
                            result.Type = ResponseType.String;
                            result.Data += GetRedisReply();
                        }
                        break;
                    case RequestType.KEYS:
                    case RequestType.HKEYS:
                    case RequestType.LRANGE:
                    case RequestType.SMEMBERS:
                    case RequestType.CLUSTER_GETKEYSINSLOT:
                        result.Type = ResponseType.Lines;
                        var sb = new StringBuilder();
                        var rn = GetRowNum(command, out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                            break;
                        }
                        if (rn > 0)
                        {
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReply(), out error);
                                sb.Append(GetRedisReply());
                            }
                        }
                        result.Data = sb.ToString();
                        break;
                    case RequestType.HGETALL:
                    case RequestType.ZRANGE:
                    case RequestType.ZREVRANGE:
                        result.Type = ResponseType.KeyValues;
                        sb = new StringBuilder();
                        rn = GetRowNum(command, out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                            break;
                        }
                        if (rn > 0)
                        {
                            for (int i = 0; i < rn; i++)
                            {
                                len = GetWordsNum(GetRedisReply(), out error);
                                sb.Append(GetRedisReply());
                            }
                        }
                        result.Data = sb.ToString();
                        break;
                    case RequestType.DBSIZE:
                    case RequestType.EXISTS:
                    case RequestType.EXPIRE:
                    case RequestType.PERSIST:
                    case RequestType.SETNX:
                    case RequestType.HEXISTS:
                    case RequestType.HLEN:
                    case RequestType.LLEN:
                    case RequestType.LPUSH:
                    case RequestType.RPUSH:
                    case RequestType.LREM:
                    case RequestType.SADD:
                    case RequestType.SCARD:
                    case RequestType.SISMEMBER:
                    case RequestType.SREM:
                    case RequestType.ZADD:
                    case RequestType.ZCARD:
                    case RequestType.ZCOUNT:
                    case RequestType.ZREM:
                    case RequestType.PUBLISH:
                    case RequestType.CLUSTER_KEYSLOT:
                    case RequestType.CLUSTER_COUNTKEYSINSLOT:
                        var val = GetValue(command, out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                            break;
                        }
                        if (val == 0)
                        {
                            result.Type = ResponseType.Empty;
                        }
                        else
                        {
                            result.Type = ResponseType.OK;
                        }
                        result.Data = val.ToString();
                        break;
                    case RequestType.INFO:
                    case RequestType.CLUSTER_INFO:
                    case RequestType.CLUSTER_NODES:
                        var rnum = GetWordsNum(command, out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                            break;
                        }
                        var info = "";
                        while (info.Length < rnum)
                        {
                            info += GetRedisReply();
                        }
                        result.Type = ResponseType.String;
                        result.Data = info;
                        break;
                    case RequestType.SUBSCRIBE:
                        var r = "";
                        while (IsSubed)
                        {
                            r = GetRedisReply();
                            if (r == "message\r\n")
                            {
                                result.Type = ResponseType.Sub;
                                GetRedisReply();
                                result.Data = GetRedisReply();
                                GetRedisReply();
                                result.Data += GetRedisReply();
                                break;
                            }
                        }
                        break;
                    case RequestType.UNSUBSCRIBE:
                        var rNum = GetRowNum(command, out error);
                        var wNum = GetWordsNum(GetRedisReply(), out error);
                        GetRedisReply();
                        wNum = GetWordsNum(GetRedisReply(), out error);
                        var channel = GetRedisReply();
                        var vNum = GetValue(GetRedisReply(), out error);
                        IsSubed = false;
                        break;
                    case RequestType.SCAN:
                    case RequestType.HSCAN:
                    case RequestType.SSCAN:
                    case RequestType.ZSCAN:
                        result.Type = ResponseType.Lines;
                        while (command == _enter)
                        {
                            command = GetRedisReply();
                        }
                        sb = new StringBuilder();

                        int offset = 0;

                        rn = GetRowNum(command, out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            result.Type = ResponseType.Error;
                            result.Data = error;
                            return result;
                        }
                        if (rn > 0)
                        {
                            len = GetWordsNum(GetRedisReply(), out error);
                            int.TryParse(GetRedisReply(), out offset);
                            sb.AppendLine("offset:" + offset);
                        }
                        //
                        command = GetRedisReply();
                        if (string.IsNullOrEmpty(command))
                        {
                            break;
                        }

                        rn = GetRowNum(command, out error);

                        for (int i = 0; i < rn; i++)
                        {
                            command = GetRedisReply();
                            len = GetWordsNum(command, out error);
                            if (len >= 0)
                            {
                                sb.Append(GetRedisReply());
                            }
                        }
                        result.Data = sb.ToString();
                        break;
                    default:
                        result.Type = ResponseType.Undefined;
                        result.Data = "未知的命令，请自行添加解码规则！";
                        break;
                }
            }

            _autoResetEvent.Set();

            return result;
        }

        private static bool GetStatus(string command, out string error)
        {
            error = string.Empty;
            var result = false;
            if (!string.IsNullOrEmpty(command) && command.Length > 0)
            {
                var c = command.Substring(0, 1);
                if (c == "+")
                {
                    result = true;
                }
                else
                {
                    error = command.Substring(1);
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
                if (string.Compare(command, "+") == 1)
                {
                    result = true;
                }
                else
                {
                    result = false;
                }
                msg = command.Substring(1, command.Length - 3);
            }
            return result;
        }

        private static int GetRowNum(string command, out string error)
        {
            error = "";
            int num = -1;

            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.Substring(0, 1) == "*")
                {
                    num = int.Parse(command.Substring(1));
                }
                if (command.Length > 2 && command.Substring(0, 1) == "-")
                {
                    error = command.Substring(1);
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
                if (command.Length > 2 && command.Substring(0, 1) == "$")
                {
                    num = int.Parse(command.Substring(1));
                }
                if (command.Length > 2 && command.Substring(0, 1) == "-")
                {
                    error = command.Substring(1);
                }
            }

            return num;
        }

        private static int GetValue(string command, out string error)
        {
            error = "";
            int num = 0;
            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.Substring(0, 1) == ":")
                {
                    int.TryParse(command.Substring(1), out num);
                }
                if (command.Length > 2 && command.Substring(0, 1) == "-")
                {
                    error = command.Substring(1);
                }
            }

            return num;
        }

        private static ResponseData Redirect(string command)
        {
            ResponseData result = null;
            if (command.IndexOf("-MOVED") == 0)
            {
                result = new ResponseData()
                {
                    Type = ResponseType.Redirect,
                    Data = command.Split(" ")[2].Replace("\r\n", "")
                };
            }
            return result;
        }

        public void Dispose()
        {
            //_queue.Clear();
            _queue = null;
        }

    }
}
