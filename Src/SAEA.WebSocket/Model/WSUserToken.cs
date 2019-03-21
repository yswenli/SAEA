/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： v4.3.1.2
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
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.WebSocket.Model
{
    class WSUserToken : UserToken, IUserToken
    {
        protected const string CrLf = "\r\n";

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
                //responseBuilder.AppendFormat("Sec-WebSocket-Protocol: {0}\r\n", "wenli.asea");
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
        /// <param name="serverIP"></param>
        /// <param name="serverPort"></param>
        /// <returns></returns>
        public static byte[] RequestHandShark(string serverIP, int serverPort)
        {
            var sb = new StringBuilder(64);
            sb.AppendFormat("{0} ws:{1}:{2} HTTP/{3}{4}", "GET", serverIP, serverPort, "1.1", CrLf);
            sb.AppendFormat("{0}: {1}{2}", "Upgrade", "websocket", CrLf);
            sb.AppendFormat("{0}: {1}{2}", "Connection", "Upgrade", CrLf);
            sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Key", CreateBase64Key(), CrLf);
            sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Protocol", "wenli.asea", CrLf);
            sb.AppendFormat("{0}: {1}{2}", "Sec-WebSocket-Version", "13", CrLf);
            sb.Append(CrLf);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
