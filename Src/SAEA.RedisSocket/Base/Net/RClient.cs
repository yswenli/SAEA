﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RClient
*版本号： v7.0.0.1
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Base;
using SAEA.Sockets.Core.Tcp;
using System;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// 异步代理
    /// </summary>
    /// <param name="active"></param>
    /// <returns></returns>
    public delegate Task OnActionHandler(DateTime active);

    internal class RClient : IocpClientSocket
    {
        /// <summary>
        /// 当接收到消息时触发的事件
        /// </summary>
        public event Action<byte[]> OnMessage;

        /// <summary>
        /// 当客户端被激活时触发的事件
        /// </summary>
        public event OnActionHandler OnActived;

        /// <summary>
        /// 同步对象
        /// </summary>
        public readonly object SyncRoot;

        /// <summary>
        /// 初始化 RClient 类的新实例
        /// </summary>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="ip">IP 地址</param>
        /// <param name="port">端口号</param>
        public RClient(int bufferSize = 64 * 1024, string ip = "127.0.0.1", int port = 6379)
            : base(new BaseContext<RedisCoder>(), string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip, port, bufferSize)
        {
            SyncRoot = new object();
        }

        /// <summary>
        /// 当接收到数据时调用
        /// </summary>
        /// <param name="data">接收到的数据</param>
        protected override void OnReceived(byte[] data)
        {
            OnMessage?.Invoke(data);
            OnActived?.Invoke(DateTimeHelper.Now);
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="cmd">请求命令</param>
        public void Request(byte[] cmd)
        {
            SendAsync(cmd);
            OnActived?.Invoke(DateTimeHelper.Now);
        }
    }
}
