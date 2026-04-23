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
*命名空间：SAEA.Sockets.Base
*文件名： BaseUserToken
*版本号： v26.4.23.1
*唯一标识：26c02e3f-743e-4e02-b007-5a1d1c447f96
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/21 19:42:03
*描述：BaseUserToken令牌类
*
*=====================================================================
*修改标记
*修改时间：2019/08/21 19:42:03
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BaseUserToken令牌类
*
*****************************************************************************/
using System;
using System.Net.Sockets;
using System.Threading;

using SAEA.Common;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    public class BaseUserToken : IUserToken
    {
        AutoResetEvent _writeAutoResetEvent = new AutoResetEvent(true);
        bool _isSending = false;

        public BaseUserToken()
        {
            _writeAutoResetEvent = new AutoResetEvent(true);
            Guid = System.Guid.NewGuid().ToString("N");
        }

        public string Guid { get; private set; }

        public string ID { get; set; }

        public Socket Socket { get; set; }

        public SocketAsyncEventArgs ReadArgs { get; set; }

        public SocketAsyncEventArgs WriteArgs { get; set; }

        public DateTime Linked { get; set; }

        public DateTime Actived { get; set; }

        public ICoder Coder { get; set; }

        public bool IsSending
        {
            get { return _isSending; }
            set { _isSending = value; }
        }

        public bool WaitWrite(int timeout)
        {
            return _writeAutoResetEvent.WaitOne(timeout);
        }

        public void ReleaseWrite()
        {
            _writeAutoResetEvent.Set();
        }

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