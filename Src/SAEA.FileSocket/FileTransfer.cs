/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.FileSocket
*文件名： Class1
*版本号： V2.2.2.0
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
*版本号： V2.2.2.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.IO;
using System.Threading;

namespace SAEA.FileSocket
{
    public class FileTransfer
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

        public event Action<string> OnReceiveEnd;

        bool _connected = false;


        public event Action<string> OnDisplay;

        public FileTransfer(string filePath, int bufferSize = 100 * 1024)
        {
            _filePath = filePath;
            _bufferSize = bufferSize;

            _receiver = new Server(_bufferSize);
            _receiver.OnRequested += _receiver_OnRequested;
            _receiver.OnFile += _receiver_OnFile;
            _receiver.OnError += _receiver_OnError;
            _receiver.Start();

            this.ShowMonitorInfo();
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

        public void SendFile(string fileName, string ip)
        {
            _beginSend = false;

            if (!_connected)
            {
                _sender = new Client(_bufferSize, ip);

                ConsoleHelper.WriteLine("正在连接IP:{0}...", ip);

                _sender.ConnectAsync((state) =>
                {
                    if (state == System.Net.Sockets.SocketError.Success)
                    {
                        _connected = true;
                        SendFileBase(fileName, ip);
                    }
                    else
                    {
                        ConsoleHelper.WriteLine("连接IP:{0}失败", ip);
                    }
                });
            }
            else
            {
                SendFileBase(fileName, ip);
            }
        }

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
            new Thread(new ThreadStart(() =>
            {
                string result;

                long oldSended = 0;
                long oldRecevied = 0;

                long s_speed = 0;
                long r_speed = 0;

                while (true)
                {
                    result = string.Empty;

                    while (true)
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
                    Thread.Sleep(1000);
                }
            }))
            { IsBackground = true }.Start();
        }







    }
}
