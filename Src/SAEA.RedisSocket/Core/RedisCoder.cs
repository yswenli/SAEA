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
using SAEA.Commom;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

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
            return sb.ToString();
        }

        /// <summary>
        /// 接收来自RedisServer的命令
        /// </summary>
        /// <param name="command"></param>
        public void Enqueue(string command)
        {
            _queue.Enqueue(command);
        }


        private string BlockDequeue()
        {
            var result = string.Empty;
            do
            {
                if (!_queue.TryDequeue(out result))
                {
                    Thread.Sleep(0);
                }
            }
            while (string.IsNullOrEmpty(result));
            return result;
        }

        public bool IsSubed = false;

        public ResponseData Decoder()
        {
            var result = new ResponseData();

            string command = null;

            string error = null;

            var len = 0;

            switch (_commandName)
            {
                case RequestType.PING:
                    command = BlockDequeue();
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
                    command = BlockDequeue();
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
                    command = BlockDequeue();
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
                    len = GetWordsNum(BlockDequeue(), out error);
                    if (len == -1)
                    {
                        result.Type = ResponseType.Empty;
                        result.Data = error;
                    }
                    else
                    {
                        result.Type = ResponseType.String;
                        result.Data += BlockDequeue();
                    }
                    break;
                case RequestType.KEYS:
                case RequestType.HKEYS:
                case RequestType.LRANGE:
                case RequestType.SMEMBERS:
                    result.Type = ResponseType.Lines;
                    var sb = new StringBuilder();
                    var rn = GetRowNum(BlockDequeue(), out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        result.Type = ResponseType.Error;
                        result.Data = error;
                        break;
                    }
                    //再尝试读取一次，发现有回车行出现
                    if (rn == -1) rn = GetRowNum(BlockDequeue(), out error);
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
                            len = GetWordsNum(BlockDequeue(), out error);
                            sb.AppendLine(BlockDequeue());
                        }
                    }
                    result.Data = sb.ToString();
                    break;
                case RequestType.HGETALL:
                case RequestType.ZRANGE:
                case RequestType.ZREVRANGE:
                    result.Type = ResponseType.KeyValues;
                    sb = new StringBuilder();
                    rn = GetRowNum(BlockDequeue(), out error);
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
                            len = GetWordsNum(BlockDequeue(), out error);
                            sb.AppendLine(BlockDequeue());
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
                    var val = GetValue(BlockDequeue(), out error);
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
                    var rnum = GetWordsNum(BlockDequeue(), out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        result.Type = ResponseType.Error;
                        result.Data = error;
                        break;
                    }
                    var info = "";
                    while (info.Length < rnum)
                    {
                        info += BlockDequeue();
                    }
                    result.Type = ResponseType.String;
                    result.Data = info;
                    break;
                case RequestType.SUBSCRIBE:
                    var r = "";
                    while (IsSubed)
                    {
                        r = BlockDequeue();
                        if (r == "message\r\n")
                        {
                            result.Type = ResponseType.Sub;
                            BlockDequeue();
                            result.Data = BlockDequeue();
                            BlockDequeue();
                            result.Data += BlockDequeue();
                            break;
                        }
                    }
                    break;
                case RequestType.UNSUBSCRIBE:
                    var rNum = GetRowNum(BlockDequeue(), out error);
                    var wNum = GetWordsNum(BlockDequeue(), out error);
                    BlockDequeue();
                    wNum = GetWordsNum(BlockDequeue(), out error);
                    var channel = BlockDequeue();
                    var vNum = GetValue(BlockDequeue(), out error);
                    IsSubed = false;
                    break;
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
                var c = command.Substring(0, 1);
                if (c == "+")
                {
                    result = true;
                }
                else
                {
                    result = false;
                }
                msg = command.Substring(1);
            }
            return result;
        }

        private static int GetRowNum(string command, out string msg)
        {
            msg = "";
            int num = -1;

            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.Substring(0, 1) == "*")
                {
                    num = int.Parse(command.Substring(1));
                }
                if (command.Length > 2 && command.Substring(0, 1) == "-")
                {
                    msg = command.Substring(1);
                }
            }
            return num;
        }

        private static int GetWordsNum(string command, out string msg)
        {
            msg = "";
            int num = -1;
            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.Substring(0, 1) == "$")
                {
                    num = int.Parse(command.Substring(1));
                }
                if (command.Length > 2 && command.Substring(0, 1) == "-")
                {
                    msg = command.Substring(1);
                }
            }

            return num;
        }

        private static int GetValue(string command, out string msg)
        {
            msg = "";
            int num = 0;
            if (!string.IsNullOrEmpty(command))
            {
                if (command.Length > 2 && command.Substring(0, 1) == ":")
                {
                    int.TryParse(command.Substring(1), out num);
                }
                if (command.Length > 2 && command.Substring(0, 1) == "-")
                {
                    msg = command.Substring(1);
                }
            }

            return num;
        }

        public void Dispose()
        {
            _queue.Clear();
            _queue = null;
        }

    }
}
