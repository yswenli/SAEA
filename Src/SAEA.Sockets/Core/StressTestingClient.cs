/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： StressTestingClient
*版本号： V1.0.0.0
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/9/8 20:00:00
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/9/8 20:00:00
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 压力测试客户端
    /// </summary>
    public sealed class StressTestingClient
    {
        SocketAsyncEventArgs connectArgs;

        List<Socket> _list;

        IPEndPoint serverEndPoint;

        int _connections = 0;
        public int Connections => _connections;

        public int Lost { get; private set; }

        bool isStop = false;

        AutoResetEvent _connectEvent = new AutoResetEvent(true);


        public StressTestingClient(int count = 10 * 1000, string ip = "127.0.0.1", int port = 39654)
        {
            serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            connectArgs = new SocketAsyncEventArgs();
            connectArgs.RemoteEndPoint = serverEndPoint;
            connectArgs.Completed += ConnectArgs_Completed;

            _list = new List<Socket>(count);
            for (int i = 0; i < count; i++)
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.NoDelay = true;
                _list.Add(socket);
            }

            Calc();
        }

        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnected(e);
        }

        void ProcessConnected(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _connections);
            _connectEvent.Set();
        }

        void Calc()
        {
            ThreadHelper.Run(() =>
            {
                while (!isStop)
                {
                    this.Lost = _list.Where(b => !b.Connected).Count();
                    Thread.Sleep(500);
                }
            });
        }

        public void Connect()
        {
            foreach (var socket in _list)
            {
                _connectEvent.WaitOne();
                if (!socket.ConnectAsync(connectArgs))
                {
                    ProcessConnected(connectArgs);
                }
            }
        }

        public void Clear()
        {
            isStop = true;
            foreach (var item in _list)
            {
                item.Close();
            }
            _list.Clear();
        }
    }
}
