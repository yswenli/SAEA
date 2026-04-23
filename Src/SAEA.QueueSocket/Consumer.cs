/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket
*文件名： Consumer
*版本号： v26.4.23.1
*唯一标识：1de15eb9-a067-4d64-88b6-ddf478885902
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/10/23 17:38:33
*描述：Consumer消费者类
*
*=====================================================================
*修改标记
*修改时间：2023/10/23 17:38:33
*修改人： yswenli
*版本号： v26.4.23.1
*描述：Consumer消费者类
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Threading;
using SAEA.QueueSocket.Model;
using SAEA.Sockets.Handler;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 消费者
    /// </summary>
    public class Consumer : IDisposable
    {

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        public event Action<QueueMsg> OnMessage;


        QClient _consumer;

        // 定期清理任务取消令牌源
        private CancellationTokenSource _cleanupCts;

        /// <summary>
        /// 队列主题
        /// </summary>
        public string Topic { get; private set; }

        public bool Connected
        {
            get
            {
                return _consumer.Connected;
            }
        }

        /// <summary>
        /// 消费者
        /// </summary>
        /// <param name="name"></param>
        /// <param name="serverAddress"></param>
        public Consumer(string name, string serverAddress)
        {
            _consumer = new QClient(name, serverAddress);
            _consumer.OnMessage += _consumer_OnMessage;
            _consumer.OnDisconnected += _consumer_OnDisconnected;
            _consumer.OnError += _consumer_OnError;
        }


        private void _consumer_OnMessage(QueueMsg obj)
        {
            if (obj != null)
            {
                OnMessage?.Invoke(obj);
            }
        }

        private void _consumer_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnDisconnected?.Invoke(id, ex);
        }
        private void _consumer_OnError(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// 订阅队列主题
        /// </summary>
        /// <param name="topic"></param>
        public void Subscribe(string topic)
        {
            Topic = topic;
        }

        /// <summary>
        /// 启动
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Start()
        {
            if (string.IsNullOrEmpty(Topic)) throw new Exception("消费者启动前请先订阅队列主题");
            _consumer.Connect();
            _consumer.Subscribe(Topic);
            StartCleanupTask(); // 启动清理任务
        }

        /// <summary>
        /// 启动定期清理任务
        /// </summary>
        private void StartCleanupTask()
        {
            _cleanupCts = new CancellationTokenSource();
            TaskHelper.LongRunning(() =>
            {
                while (!_cleanupCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        _consumer?.ClearCoderBuffer();
                        Thread.Sleep(60000); // 每分钟清理一次
                    }
                    catch { }
                }
            });
        }

        /// <summary>
        /// 停止清理任务
        /// </summary>
        private void StopCleanupTask()
        {
            _cleanupCts?.Cancel();
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            if (_consumer.Connected)
            {
                _consumer.Unsubscribe(Topic);
                _consumer.Disconnect();
            }
        }

        public void Dispose()
        {
            StopCleanupTask();
            Stop();
            // 取消事件订阅
            _consumer.OnMessage -= _consumer_OnMessage;
            _consumer.OnDisconnected -= _consumer_OnDisconnected;
            _consumer.OnError -= _consumer_OnError;
            _consumer?.Dispose();
        }
    }
}