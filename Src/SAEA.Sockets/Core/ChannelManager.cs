/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： ChannelManager
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
using SAEA.Common;
using SAEA.Sockets.Model;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{
    public class ChannelManager
    {
        ConcurrentDictionary<string, ChannelInfo> _concurrentDictionary = new ConcurrentDictionary<string, ChannelInfo>();

        int _timeOut = 60;

        protected ChannelManager(int timeOut = 60)
        {
            _timeOut = timeOut;
        }

        public ChannelInfo Set(string id, Socket socket, Stream stream)
        {
            var ci = new ChannelInfo()
            {
                ID = id,
                ClientSocket = socket,
                Stream = stream,
                Expired = DateTimeHelper.Now.AddSeconds(_timeOut)
            };
            _concurrentDictionary.AddOrUpdate(id, ci, (k, v) => ci);
            return ci;
        }


        public ChannelInfo Get(string id)
        {
            if (_concurrentDictionary.TryGetValue(id, out ChannelInfo ci))
            {
                if (ci != null)
                {
                    if (ci.Expired < DateTimeHelper.Now)
                        Remove(id);
                    else
                        return ci;
                }
            }
            return null;
        }


        public void Refresh(string id)
        {
            if (_concurrentDictionary.TryGetValue(id, out ChannelInfo ci))
            {
                if (ci != null)

                    ci.Expired = DateTimeHelper.Now.AddSeconds(_timeOut);
            }
        }


        public void Remove(string id)
        {
            _concurrentDictionary.TryRemove(id, out ChannelInfo ci);
        }


        public void Clear()
        {
            var vals = _concurrentDictionary.Values;

            if (vals != null && vals.Any())
            {
                foreach (var item in vals)
                {
                    try
                    {
                        item.ClientSocket.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    item.ClientSocket.Close();
                }
            }
            _concurrentDictionary.Clear();
        }


        static ChannelManager _channelManager = null;


        public static ChannelManager Instance
        {
            get
            {
                if (_channelManager == null)
                {
                    _channelManager = new ChannelManager(int.MaxValue);
                }
                return _channelManager;
            }
        }

        public static ChannelManager GetInstance(int timeOut = 60)
        {
            if (_channelManager == null)
            {
                _channelManager = new ChannelManager(timeOut);
            }
            return _channelManager;
        }
    }
}
