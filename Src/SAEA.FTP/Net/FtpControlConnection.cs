/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.FTP.Net
*文件名： FtpControlConnection
*版本号： v26.11.5.1
*唯一标识：9811ff44-df7f-4dc1-9d1f-ffa709309264
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:37:27
*描述：FTP客户端控制连接（同步、支持多行响应、EPSV/PASV、REST）
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using SAEA.FTP.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SAEA.FTP.Net
{
    /// <summary>
    /// FTP 响应
    /// </summary>
    public sealed class FtpReply
    {
        /// <summary>
        /// 三位响应码
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 多行响应时，各行以 \n 拼接
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 未经解析的原始响应文本，多行以 \n 连接
        /// </summary>
        public string Raw { get; set; } = string.Empty;

        /// <summary>
        /// 是否为 2xx 成功响应
        /// </summary>
        public bool IsPositive
        {
            get { return Code >= 200 && Code < 300; }
        }

        /// <summary>
        /// 返回「状态码 + 空格 + 响应文本」
        /// </summary>
        /// <returns>便于日志输出的响应描述</returns>
        public override string ToString()
        {
            return Code + " " + Message;
        }
    }

    /// <summary>
    /// FTP 命令失败
    /// </summary>
    public class FTPCommandException : IOException
    {
        /// <summary>
        /// 服务器返回的响应码
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// 服务器返回的响应文本
        /// </summary>
        public string Reply { get; private set; }

        /// <summary>
        /// 以响应码与响应文本构造异常
        /// </summary>
        /// <param name="code">服务器返回的响应码</param>
        /// <param name="reply">服务器返回的响应文本</param>
        public FTPCommandException(int code, string reply)
            : base("code:" + code + ",reply:" + reply)
        {
            Code = code;
            Reply = reply;
        }
    }

    /// <summary>
    /// 本地文件/磁盘错误，不参与网络重试
    /// </summary>
    public class FTPLocalException : IOException
    {
        /// <summary>
        /// 以错误描述与内部异常构造本地错误
        /// </summary>
        /// <param name="message">错误描述</param>
        /// <param name="inner">内部异常</param>
        public FTPLocalException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// 单条 FTP 控制连接。每条并行分片使用独立实例。
    /// </summary>
    internal sealed class FtpControlConnection : IDisposable
    {
        readonly ClientConfig _config;

        readonly byte[] _readBuffer = new byte[8192];

        readonly Encoding _encoding;

        int _readPos;

        int _readLen;

        TcpClient _tcp;

        NetworkStream _stream;

        readonly object _syncRoot = new object();

        public FtpReply Greeting { get; private set; }

        public bool SupportsRest { get; private set; }

        public bool SupportsMdtm { get; private set; }

        public bool SupportsMlsd { get; private set; }

        public bool SupportsEpsv { get; private set; }

        public bool SupportsSize { get; private set; }

        public string OverwritePolicyHint { get; private set; }

        public string ControlHost
        {
            get
            {
                try
                {
                    var ep = _tcp != null ? _tcp.Client.RemoteEndPoint as IPEndPoint : null;
                    return ep != null ? ep.Address.ToString() : _config.IP;
                }
                catch
                {
                    return _config.IP;
                }
            }
        }

        public bool Connected
        {
            get { return _tcp != null && _tcp.Connected; }
        }

        public ClientConfig Config
        {
            get { return _config; }
        }

        /// <summary>
        /// 数据连接读写超时
        /// </summary>
        public TimeSpan DataTimeout { get; set; }

        TimeSpan _commandTimeout;

        /// <summary>
        /// 控制命令响应超时
        /// </summary>
        public TimeSpan CommandTimeout
        {
            get { return _commandTimeout; }
            set
            {
                _commandTimeout = value;
                ApplyStreamTimeouts();
            }
        }

        void ApplyStreamTimeouts()
        {
            if (_stream == null) return;

            var ms = (int)Math.Max(1000, _commandTimeout.TotalMilliseconds);

            try { _stream.ReadTimeout = ms; } catch { }
            try { _stream.WriteTimeout = ms; } catch { }
        }

        public FtpControlConnection(ClientConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _config = config;
            _encoding = config.Encoding ?? Encoding.UTF8;
            _commandTimeout = config.CommandTimeout > TimeSpan.Zero ? config.CommandTimeout : TimeSpan.FromSeconds(30);
            DataTimeout = config.DataTimeout > TimeSpan.Zero ? config.DataTimeout : TimeSpan.FromSeconds(60);
        }

        #region 连接与登录

        public void Connect()
        {
            Close();

            var timeout = _config.ConnectTimeout;
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(30);

            _tcp = new TcpClient();
            _tcp.NoDelay = true;
            _tcp.ReceiveBufferSize = 64 * 1024;
            _tcp.SendBufferSize = 64 * 1024;

            try
            {
                var ar = _tcp.BeginConnect(_config.IP, _config.Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeout))
                {
                    throw new TimeoutException("FTP connect timeout: " + _config.IP + ":" + _config.Port);
                }
                _tcp.EndConnect(ar);
            }
            catch
            {
                Close();
                throw;
            }

            _stream = _tcp.GetStream();

            ApplyStreamTimeouts();

            Greeting = ReadReply();

            if (Greeting.Code != ServerResponseCode.服务就绪)
            {
                Close();
                throw new FTPCommandException(Greeting.Code, Greeting.Message);
            }

            try
            {
                Login();

                Feat();

                try
                {
                    Send("OPTS UTF8 ON");
                    // 忽略不支持 UTF8 的服务器
                }
                catch
                {
                }

                try
                {
                    var typeReply = Send("TYPE I");
                    if (typeReply.Code != ServerResponseCode.成功)
                    {
                        // 少数服务器拒绝 TYPE I，继续按字节传输
                    }
                }
                catch
                {
                }
            }
            catch
            {
                Close();
                throw;
            }
        }

        void Login()
        {
            var reply = Send(FTPCommand.USER + " " + _config.UserName);

            if (reply.Code != ServerResponseCode.登录成功 && reply.Code != ServerResponseCode.要求密码)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }

            if (reply.Code == ServerResponseCode.要求密码)
            {
                reply = Send(FTPCommand.PASS + " " + _config.Password);

                if (reply.Code != ServerResponseCode.登录成功 && reply.Code != ServerResponseCode.初始命令没有执行)
                {
                    throw new FTPCommandException(reply.Code, reply.Message);
                }
            }
        }

        void Feat()
        {
            try
            {
                var reply = Send(FTPCommand.FEAT.ToString());

                if (reply.Code != ServerResponseCode.系统状态回复) return;

                var text = reply.Message ?? string.Empty;

                SupportsRest = Contains(text, "REST STREAM");
                SupportsSize = Contains(text, " SIZE") || text.StartsWith("SIZE", StringComparison.OrdinalIgnoreCase);
                SupportsMdtm = Contains(text, "MDTM");
                SupportsMlsd = Contains(text, "MLSD");
                SupportsEpsv = Contains(text, "EPSV");

                var idx = text.IndexOf("SAEA-OVERWRITE=", StringComparison.OrdinalIgnoreCase);
                if (idx > -1)
                {
                    var start = idx + "SAEA-OVERWRITE=".Length;
                    var end = text.IndexOfAny(new[] { '\r', '\n', ' ' }, start);
                    OverwritePolicyHint = end > start ? text.Substring(start, end - start) : text.Substring(start);
                }

                // FEAT 不可靠时按保守值处理
                if (!SupportsRest && !Contains(text, "REST"))
                {
                    SupportsRest = false;
                }
            }
            catch
            {
            }
        }

        static bool Contains(string text, string value)
        {
            return text != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) > -1;
        }

        #endregion

        #region 命令与响应

        public FtpReply Send(string command)
        {
            lock (_syncRoot)
            {
                if (!Connected)
                {
                    throw new IOException("FTP control connection is closed");
                }

                var data = _encoding.GetBytes(command + "\r\n");

                try
                {
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
                catch (Exception ex)
                {
                    throw new IOException("FTP command send failed: " + command, ex);
                }

                return ReadReply();
            }
        }

        public FtpReply SendExpect(string command, params int[] expected)
        {
            var reply = Send(command);

            if (expected == null || expected.Length == 0) return reply;

            foreach (var code in expected)
            {
                if (reply.Code == code) return reply;
            }

            throw new FTPCommandException(reply.Code, reply.Message);
        }

        int ReadByte()
        {
            if (_readPos >= _readLen)
            {
                _readLen = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                _readPos = 0;

                if (_readLen <= 0) return -1;
            }

            return _readBuffer[_readPos++];
        }

        string ReadLine()
        {
            var bytes = new List<byte>(128);

            while (true)
            {
                var value = ReadByte();

                if (value < 0)
                {
                    if (bytes.Count == 0) throw new IOException("FTP control connection closed by server");
                    break;
                }

                if (value == '\n')
                {
                    if (bytes.Count > 0 && bytes[bytes.Count - 1] == '\r')
                    {
                        bytes.RemoveAt(bytes.Count - 1);
                    }
                    break;
                }

                bytes.Add((byte)value);
            }

            return _encoding.GetString(bytes.ToArray());
        }

        public FtpReply ReadReply()
        {
            var first = ReadLine();

            if (first.Length < 3)
            {
                throw new IOException("Malformed FTP reply: " + first);
            }

            int code;
            if (!int.TryParse(first.Substring(0, 3), out code))
            {
                throw new IOException("Malformed FTP reply: " + first);
            }

            var message = first.Length > 4 ? first.Substring(4) : string.Empty;
            var raw = first;

            if (first.Length > 3 && first[3] == '-')
            {
                var terminator = code.ToString() + " ";
                var sb = new StringBuilder();
                sb.Append(message);

                while (true)
                {
                    var line = ReadLine();
                    raw += "\n" + line;

                    if (line.StartsWith(terminator, StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }

                message = sb.ToString();
            }

            return new FtpReply
            {
                Code = code,
                Message = message,
                Raw = raw
            };
        }

        #endregion

        #region 数据连接

        /// <summary>
        /// 打开被动模式数据连接，优先 EPSV，失败回退 PASV。
        /// </summary>
        public TcpClient OpenPassive(TimeSpan timeout)
        {
            if (SupportedEpsv())
            {
                try
                {
                    var reply = Send(FTPCommand.EPSV.ToString());
                    if (reply.Code == ServerResponseCode.进入扩展被动模式)
                    {
                        return ConnectData(ControlHost, ParseEpsvPort(reply.Message), timeout);
                    }
                }
                catch (FTPCommandException)
                {
                    // 回退 PASV
                }
            }

            var pasv = Send(FTPCommand.PASV.ToString());

            if (pasv.Code != ServerResponseCode.进入被动模式)
            {
                throw new FTPCommandException(pasv.Code, pasv.Message);
            }

            string ip;
            int port;
            ParsePasv(pasv.Message, out ip, out port);

            if (string.IsNullOrEmpty(ip) || ip == "0.0.0.0")
            {
                ip = ControlHost;
            }

            return ConnectData(ip, port, timeout);
        }

        bool SupportedEpsv()
        {
            return SupportsEpsv;
        }

        static int ParseEpsvPort(string message)
        {
            var start = message.IndexOf('(');
            var end = message.IndexOf(')');

            if (start < 0 || end <= start)
            {
                throw new IOException("Malformed EPSV reply: " + message);
            }

            var inner = message.Substring(start + 1, end - start - 1);
            var parts = inner.Split('|');

            int port;
            if (parts.Length >= 4 && int.TryParse(parts[3], out port))
            {
                return port;
            }

            throw new IOException("Malformed EPSV reply: " + message);
        }

        static void ParsePasv(string message, out string ip, out int port)
        {
            ip = null;
            port = 0;

            var start = message.IndexOf('(');
            var end = message.IndexOf(')');

            if (start < 0 || end <= start)
            {
                throw new IOException("Malformed PASV reply: " + message);
            }

            var inner = message.Substring(start + 1, end - start - 1);
            var parts = inner.Split(',');

            if (parts.Length != 6)
            {
                throw new IOException("Malformed PASV reply: " + message);
            }

            ip = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];

            int high, low;
            if (!int.TryParse(parts[4], out high) || !int.TryParse(parts[5], out low))
            {
                throw new IOException("Malformed PASV reply: " + message);
            }

            port = (high << 8) + low;
        }

        TcpClient ConnectData(string host, int port, TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(60);

            var tcp = new TcpClient();
            tcp.NoDelay = true;
            tcp.ReceiveBufferSize = 256 * 1024;
            tcp.SendBufferSize = 256 * 1024;

            try
            {
                var ar = tcp.BeginConnect(host, port, null, null);

                if (!ar.AsyncWaitHandle.WaitOne(timeout))
                {
                    throw new TimeoutException("FTP data connect timeout: " + host + ":" + port);
                }

                tcp.EndConnect(ar);
                tcp.GetStream().ReadTimeout = (int)Math.Max(1000, DataTimeout.TotalMilliseconds);
                tcp.GetStream().WriteTimeout = (int)Math.Max(1000, DataTimeout.TotalMilliseconds);
                return tcp;
            }
            catch
            {
                try { tcp.Close(); } catch { }
                throw;
            }
        }

        #endregion

        public void Close()
        {
            try
            {
                if (_stream != null) _stream.Dispose();
            }
            catch { }

            try
            {
                if (_tcp != null) _tcp.Close();
            }
            catch { }

            _stream = null;
            _tcp = null;
            _readPos = 0;
            _readLen = 0;
        }

        public void Dispose()
        {
            Close();
        }
    }
}