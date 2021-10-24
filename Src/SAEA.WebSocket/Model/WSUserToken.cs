/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.WebSocket.Model
{
    class WSUserToken : BaseUserToken, IUserToken
    {
        protected const string Enter = "\r\n";

        public bool IsHandSharked
        {
            get; set;
        }


        List<byte> _cache = new List<byte>();

        /// <summary>
        /// 服务器回复握手
        /// </summary>
        /// <param name="handShakeBytes"></param>
        /// <returns></returns>
        public bool GetReplayHandShake(byte[] handShakeBytes, out byte[] data)
        {
            bool result = false;
            data = null;
            try
            {
                _cache.AddRange(handShakeBytes);

                string handShakeText = Encoding.UTF8.GetString(_cache.ToArray());

                string key = string.Empty;
                Regex reg = new Regex(@"Sec\-WebSocket\-Key:(.*?)\r\n");
                Match m = reg.Match(handShakeText);
                if (string.IsNullOrEmpty(m.Value)) throw new Exception("请求中不存在 Sec-WebSocket-Key");
                key = Regex.Replace(m.Value, @"Sec\-WebSocket\-Key:(.*?)\r\n", "$1").Trim();
                byte[] secKeyBytes = SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                string secKey = Convert.ToBase64String(secKeyBytes);

                var responseBuilder = new StringBuilder();
                responseBuilder.Append("HTTP/1.1 101 Switching Protocols" + "\r\n");
                responseBuilder.Append("Upgrade: websocket" + "\r\n");
                responseBuilder.Append("Connection: Upgrade" + "\r\n");

                Regex reg2 = new Regex(@"Sec\-WebSocket\-Protocol:(.*?)\r\n");
                Match m2 = reg2.Match(handShakeText);
                if (!string.IsNullOrEmpty(m2.Value))
                {
                    responseBuilder.AppendFormat("Sec-WebSocket-Protocol: {0}\r\n", SubProtocolType.Default);
                }

                responseBuilder.Append("Sec-WebSocket-Accept: " + secKey + "\r\n\r\n");
                data = Encoding.UTF8.GetBytes(responseBuilder.ToString());
                result = true;
                _cache.Clear();
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine(ex.ToString());
            }
            return result;
        }



        private static string CreateBase64Key()
        {
            var src = new byte[16];
            new Random(Environment.TickCount).NextBytes(src);
            return Convert.ToBase64String(src);
        }

        /// <summary>
        /// 客户端发起握手
        /// </summary>
        /// <param name="url"></param>
        /// <param name="serverIP"></param>
        /// <param name="serverPort"></param>
        /// <param name="subProtocol"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public static byte[] RequestHandShark(string url, string serverIP, int serverPort, string subProtocol = SubProtocolType.Default, string origin = "")
        {
            var sb = new StringBuilder(64);
            sb.AppendFormat("{0} {1} HTTP/{2}{3}", "GET", url, "1.1", Enter);
            sb.AppendFormat("{0}: {1}{2}", "Host", $"{serverIP}:{serverPort}", Enter);
            sb.AppendLine("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36");
            if (!string.IsNullOrEmpty(origin))
            {
                sb.AppendLine($"Origin: {origin}");
            }
            sb.AppendFormat("{0}: {1}{2}", "Upgrade", "websocket", Enter);
            sb.AppendFormat("{0}: {1}{2}", "Connection", "Upgrade", Enter);
            sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Version", "13", Enter);
            if (!string.IsNullOrEmpty(subProtocol))
                sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Protocol", subProtocol, Enter);
            sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Key", CreateBase64Key(), Enter);
            sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Extensions", "permessage-deflate; client_max_window_bits", Enter);
            sb.Append(Enter);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }


        static List<byte> _buffer = new List<byte>();

        /// <summary>
        /// 解析回复的握手
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool AnalysisHandSharkReply(byte[] data)
        {
            _buffer.AddRange(data);

            try
            {
                var handShakeText = Encoding.UTF8.GetString(_buffer.ToArray());
                string key = string.Empty;
                Regex reg = new Regex(@"Sec\-WebSocket\-Accept:(.*?)\r\n");
                Match m = reg.Match(handShakeText);
                if (string.IsNullOrEmpty(m.Value)) throw new Exception("回复中不存在 Sec-WebSocket-Accept");
                key = Regex.Replace(m.Value, @"Sec\-WebSocket\-Accept:(.*?)\r\n", "$1").Trim();
                _buffer.Clear();
                return true;
            }
            catch(Exception ex)
            {
                LogHelper.Error("WSUserToken.AnalysisHandSharkReply", ex);
            }
            return false;
        }
    }
}
