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
*修改时间：2026/11/6 12:20:18
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.Sockets.Interface;

using System;
using System.IO;
using System.Threading;

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
        /// 创建接收服务端并订阅其请求、数据与异常事件，发送连接在首次发送时才建立
        /// </summary>
        /// <param name="filePath">接收文件的保存目录</param>
        /// <param name="port">接收服务端的监听端口</param>
        /// <param name="bufferSize">收发缓冲区大小（字节）</param>
        public FileTransfer(string filePath, int port = 39654, int bufferSize = 100 * 1024)
        {
            _filePath = filePath;
            _bufferSize = bufferSize;

            _receiver = new Server(port, _bufferSize);
            _receiver.OnRequested += _receiver_OnRequested;
            _receiver.OnFile += _receiver_OnFile;
            _receiver.OnError += _receiver_OnError;
        }

        private void _receiver_OnRequested(string ID, string fileName, long length)
        {
            _current = 0;
            _length = length;
            _fullName = Path.Combine(_filePath, fileName);
            _fileStream = new FileStream(_fullName, FileMode.OpenOrCreate, FileAccess.Write);
            _receiver.Allow(ID);
            _beginReceive = true;


            //ConsoleHelper.WriteLine("收到文件传输请求，Y 为接收 其他为拒绝");
            //var answer = ConsoleHelper.ReadLine();
            //if (!string.IsNullOrEmpty(answer) && answer.ToUpper() == "Y")
            //{
            //    _current = 0;
            //    _length = length;
            //    _fullName = PathHelper.GetFullName(fileName);
            //    _fileStream = new FileStream(_fullName, FileMode.OpenOrCreate, FileAccess.Write);
            //    _receiver.Allow(ID);
            //    _beginReceive = true;
            //}
            //else
            //{
            //    _receiver.Refuse(ID);
            //}
        }

        private void _receiver_OnFile(IUserToken userToken, byte[] content)
        {
            _current += content.Length;

            _fileStream.Write(content, 0, content.Length);

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

        private void _receiver_OnError(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("接收文件过程发生异常，ID:{0} err:{1}", ID, ex.Message);
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
    }
}