/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： UserToken
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
using System;
using System.Net.Sockets;
using System.Threading;

using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 连接信息类
    /// </summary>
    public class BaseUserToken : IUserToken
    {
        AutoResetEvent _writeAutoResetEvent = new AutoResetEvent(true);

        /// <summary>
        /// 连接信息类
        /// </summary>
        public BaseUserToken()
        {
            _writeAutoResetEvent = new AutoResetEvent(true);
            Guid = System.Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Guid { get; private set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string ID
        {
            get; set;
        }

        /// <summary>
        /// 套接字对象
        /// </summary>
        public Socket Socket
        {
            get; set;
        }

        /// <summary>
        /// 读取操作的SocketAsyncEventArgs对象
        /// </summary>
        public SocketAsyncEventArgs ReadArgs
        {
            get; set;
        }

        /// <summary>
        /// 写入操作的SocketAsyncEventArgs对象
        /// </summary>
        public SocketAsyncEventArgs WriteArgs
        {
            get; set;
        }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime Linked
        {
            get; set;
        }

        /// <summary>
        /// 活动时间
        /// </summary>
        public DateTime Actived
        {
            get; set;
        }

        /// <summary>
        /// 编码器对象
        /// </summary>
        public ICoder Coder
        {
            get; set;
        }

        /// <summary>
        /// 等待写入操作完成
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns>是否成功</returns>
        public bool WaitWrite(int timeout)
        {
            return _writeAutoResetEvent.WaitOne(timeout);
        }

        /// <summary>
        /// 释放写入操作
        /// </summary>
        public void ReleaseWrite()
        {
            _writeAutoResetEvent.Set();
        }

        /// <summary>
        /// 清除连接信息
        /// </summary>
        public void Clear()
        {
            Socket?.Close();
            Coder?.Clear();
            _writeAutoResetEvent?.Close();
            ReadArgs?.Dispose();
            WriteArgs?.Dispose();
            Socket = null;
            ReadArgs = null;
            WriteArgs = null;
        }
    }
}
