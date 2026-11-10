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
*命名空间：SAEA.FileSocket
*文件名： FileTransfer
*版本号： v26.4.23.1
*唯一标识：2c1af496-9bb0-4c6c-a588-c922940943b3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：FileTransfer接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 11:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：接收改由处理器链接管，移除对已删除服务端事件的订阅
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.FileSocket.Model;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 文件传输器，内置接收服务端与发送客户端，可在同一实例上收发文件并输出传输进度
    /// </summary>
    public class FileTransfer : IDisposable
    {
        string _filePath;

        int _bufferSize;

        Server _receiver;

        Client _sender;

        long _current = 0;

        long _length = 0;

        FileStream _fileStream;

        bool _beginSend = false;

        bool _beginReceive = false;

        string _fullName = string.Empty;

        /// <summary>
        /// 单个文件接收完成时触发，参数为保存后的文件完整路径
        /// </summary>
        public event Action<string> OnReceiveEnd;

        bool _connected = false;

        bool _started = false;


        /// <summary>
        /// 传输进行中每秒推送一次进度文本，发送与接收状态合并在一行内
        /// </summary>
        public event Action<string> OnDisplay;

        /// <summary>
        /// 创建接收服务端并在其处理器链上追加文件接收处理器，发送连接在首次发送时才建立
        /// </summary>
        /// <param name="filePath">接收文件的保存目录</param>
        /// <param name="port">接收服务端的监听端口</param>
        /// <param name="bufferSize">收发缓冲区大小（字节）</param>
        public FileTransfer(string filePath, int port = 39654, int bufferSize = 100 * 1024)
        {
            _filePath = filePath;
            _bufferSize = bufferSize;

            _receiver = new Server(port, _bufferSize);
            _receiver.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ReceiveHandler(this));
        }

        private void OnRequested(string id, ReadOnlyMemory<byte> content)
        {
            var fileMessage = SerializeHelper.PBDeserialize<FileMessage>(content.ToArray());

            _current = 0;
            _length = fileMessage.Length;
            _fullName = Path.Combine(_filePath, fileMessage.FileName);
            _fileStream = new FileStream(_fullName, FileMode.OpenOrCreate, FileAccess.Write);
            _receiver.Allow(id);
            _beginReceive = true;
        }

        private void OnFile(ReadOnlyMemory<byte> content)
        {
            _current += content.Length;

            // netstandard2.0 的 FileStream 没有 Span 写入重载；解码结果自带数组，能取到就直接写，避免再复制一次。
            ArraySegment<byte> segment;
            if (MemoryMarshal.TryGetArray(content, out segment))
            {
                _fileStream.Write(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                var copy = content.ToArray();
                _fileStream.Write(copy, 0, copy.Length);
            }

            if (_current == _length)
            {
                _fileStream.Flush();
                _fileStream.Close();
                _current = 0;
                _length = 0;
                _beginReceive = false;
                OnReceiveEnd?.Invoke(_fullName);
            }
        }

        /// <summary>
        /// 发送文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="ip"></param>
        public void SendFile(string fileName, string ip)
        {
            _beginSend = false;

            if (!_connected)
            {
                _sender = new Client(_bufferSize, ip);

                ConsoleHelper.WriteLine("正在连接IP:{0}...", ip);

                _sender.Connect();

                if (_sender.Connected)
                {
                    _connected = true;
                    SendFileBase(fileName, ip);
                }
            }
            else
            {
                SendFileBase(fileName, ip);
            }
        }

        /// <summary>
        /// 发送文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="ip"></param>
        void SendFileBase(string fileName, string ip)
        {
            ConsoleHelper.WriteLine("成功连接到IP:{0}，正在准备发送文件", ip);
            _beginSend = true;
            _sender.SendFile(fileName, 0, (d) =>
            {
                if (d)
                    ConsoleHelper.WriteLine("发送文件已完成");
                else
                    ConsoleHelper.WriteLine("发送文件失败");
                _beginSend = false;
            });
        }


        /// <summary>
        /// 监控文件管理逻辑信息
        /// </summary>
        private void ShowMonitorInfo()
        {
            TaskHelper.LongRunning(() =>
            {
                string result = string.Empty; ;

                long oldSended = 0;
                long oldRecevied = 0;

                long s_speed = 0;
                long r_speed = 0;

                while (_started)
                {
                    if (_beginSend && _beginReceive)
                    {
                        s_speed = _sender.Out - oldSended;
                        oldSended = _sender.Out;

                        r_speed = _receiver.In - oldRecevied;
                        oldRecevied = _receiver.In;

                        result = string.Format("总数：{0} 已发送：{1} 发送速度：{2}/s 接收：{3} 接收速度：{4}/s", _receiver.Total.ToSpeedString(), _sender.Out.ToSpeedString(), s_speed.ToSpeedString(), _receiver.In.ToSpeedString(), r_speed.ToSpeedString());
                    }
                    else if (_beginSend)
                    {
                        s_speed = _sender.Out - oldSended;
                        oldSended = _sender.Out;

                        result = string.Format("总数：{0} 发送：{1} 发送速度：{2}/s", _sender.Total.ToSpeedString(), _sender.Out.ToSpeedString(), s_speed.ToSpeedString());
                    }
                    else if (_beginReceive)
                    {
                        r_speed = _receiver.In - oldRecevied;
                        oldRecevied = _receiver.In;
                        result = string.Format("总数：{0} 接收：{1} 接收速度：{2}/s", _receiver.Total.ToSpeedString(), _receiver.In.ToSpeedString(), r_speed.ToSpeedString());
                    }
                    else
                    {
                        break;
                    }

                    OnDisplay?.Invoke(result);

                    Thread.Sleep(1000);
                }
            });
        }

        /// <summary>
        /// 启动
        /// </summary>
        public void Start()
        {
            if (!_started)
            {
                _started = true;

                _receiver.Start();

                ShowMonitorInfo();
            }
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Stop()
        {
            _started = false;

            _receiver.Stop();

            _sender.Disconnect();
        }

        /// <summary>
        /// 释放资源，等于停止接收服务端并断开已建立的发送连接
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 接收处理器：把传输请求与文件块分派给所属传输器，其它消息继续交给下游
        /// </summary>
        private sealed class ReceiveHandler : InboundHandlerAdapter
        {
            private readonly FileTransfer _owner;

            /// <summary>
            /// 绑定所属传输器
            /// </summary>
            /// <param name="owner">所属传输器实例</param>
            public ReceiveHandler(FileTransfer owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 按帧类型处理：请求帧回复允许并建文件，文件块帧落盘，其余消息交给下游
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解帧的消息</param>
            /// <returns>处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as BaseSocketProtocal;
                if (protocal != null && protocal.Type == (byte)SocketProtocalType.RequestSend)
                {
                    _owner.OnRequested(ctx.Channel.Id, protocal.Content);
                    return;
                }

                if (protocal != null && protocal.Type == (byte)SocketProtocalType.BigData)
                {
                    _owner.OnFile(protocal.Content);
                    return;
                }

                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
        }
    }
}