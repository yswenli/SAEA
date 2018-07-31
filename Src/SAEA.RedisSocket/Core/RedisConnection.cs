/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnection
*版本号： V1.0.0.0
*唯一标识：f02f9b69-4b98-401f-8a0d-8d13467ea1df
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/19 15:37:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/19 15:37:01
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Net;
using System;
using System.Threading;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis连接类
    /// </summary>
    public class RedisConnection : IDisposable
    {
        object _syncLocker = new object();

        RConnection _cnn;

        DateTime _actived;

        public DateTime Actived
        {
            get
            {
                return _actived;
            }
        }

        RedisCoder _redisCoder;

        public RedisCoder RedisCoder
        {
            get
            {
                return _redisCoder;
            }
        }

        public bool IsConnected { get; private set; } = false;

        bool _debugMode = false;

        public RedisConnection(string ipPort, bool debugMode = false)
        {
            var address = ipPort.GetIPPort();
            _cnn = new RConnection(102400, address.Item1, address.Item2);
            _cnn.OnActived += _cnn_OnActived;
            _cnn.OnMessage += _cnn_OnMessage;
            _redisCoder = new RedisCoder();
            _debugMode = debugMode;
        }

        private void _cnn_OnActived(DateTime actived)
        {
            _actived = actived;
        }
        private void _cnn_OnMessage(string command)
        {
            _redisCoder.Enqueue(command);
            if (_debugMode)
                ConsoleHelper.WriteLine(command);
        }

        /// <summary>
        /// 连接到redisServer
        /// </summary>
        public void Connect()
        {
            lock (_syncLocker)
            {
                if (!IsConnected)
                {
                    var autoResetEvent = new AutoResetEvent(false);

                    _cnn.ConnectAsync((s) =>
                    {
                        if (s == System.Net.Sockets.SocketError.Success)
                        {
                            IsConnected = true;
                        }
                        autoResetEvent.Set();
                    });
                    var result = autoResetEvent.WaitOne(10 * 1000);
                    if (!result || !IsConnected)
                    {
                        _cnn.Disconnect();
                        throw new Exception("无法连接到redis server!");
                    }
                }
            }
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="cmd"></param>
        public void Send(string cmd)
        {
            if (IsConnected)
            {
                _cnn.Send(cmd);
            }
        }

        /// <summary>
        /// 保持连接
        /// </summary>
        /// <param name="action"></param>
        public void KeepAlived(Action action)
        {
            action();
        }
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Quit()
        {
            if (IsConnected)
            {
                _cnn.Disconnect();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            IsConnected = false;
            _cnn.Dispose();
        }
    }
}
