﻿/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2022yswenli All Rights Reserved.
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
        AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        /// <summary>
        /// 连接信息类
        /// </summary>
        public BaseUserToken()
        {
            _autoResetEvent = new AutoResetEvent(true);
            Guid = System.Guid.NewGuid().ToString("N");
        }

        public string Guid { get; private set; }

        public string ID
        {
            get; set;
        }
        public Socket Socket
        {
            get; set;
        }

        public SocketAsyncEventArgs ReadArgs
        {
            get; set;
        }

        public SocketAsyncEventArgs WriteArgs
        {
            get; set;
        }

        public DateTime Linked
        {
            get; set;
        }

        public DateTime Actived
        {
            get; set;
        }

        public IUnpacker Unpacker
        {
            get; set;
        }

        public bool WaitOne(int timeout)
        {
            return _autoResetEvent.WaitOne(timeout);
        }


        public void Set()
        {
            _autoResetEvent.Set();
        }


        public void Clear()
        {
            Socket?.Close();
            Unpacker?.Clear();
            _autoResetEvent?.Close();
            ReadArgs?.Dispose();
            WriteArgs?.Dispose();
            Socket = null;
            ReadArgs = null;
            WriteArgs = null;
        }
    }
}
