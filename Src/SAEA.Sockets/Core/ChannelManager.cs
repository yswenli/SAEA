/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： ChannelManager
*版本号： v4.3.2.5
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
*版本号： v4.3.2.5
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Model;
using System;
using System.Collections.Concurrent;
using System.IO;
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

        public void Set(string id, Socket socket, Stream stream)
        {
            var ci = new ChannelInfo()
            {
                ID = id,
                ClientSocket = socket,
                Stream = stream,
                Expired = DateTime.Now.AddSeconds(_timeOut)
            };
            _concurrentDictionary.AddOrUpdate(id, ci, (k, v) => ci);
        }


        public ChannelInfo Get(string id)
        {
            if (_concurrentDictionary.TryGetValue(id, out ChannelInfo ci))
            {
                if (ci.Expired < DateTime.Now)
                    Remove(id);
                else
                    return ci;
            }
            return null;
        }


        public void Refresh(string id)
        {
            if (_concurrentDictionary.TryGetValue(id, out ChannelInfo ci))
            {
                ci.Expired = DateTime.Now.AddSeconds(_timeOut);
            }
        }


        public void Remove(string id)
        {
            _concurrentDictionary.TryRemove(id, out ChannelInfo ci);
        }


        public void Clear()
        {
            _concurrentDictionary.Clear();
        }


        static ChannelManager _channelManager = null;

        public static ChannelManager Current
        {
            get
            {
                if (_channelManager == null)
                {
                    _channelManager = new ChannelManager();
                }
                return _channelManager;
            }
        }
    }
}
