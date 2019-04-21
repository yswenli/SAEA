/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： v4.5.1.2
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
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.EventArgs;
using SAEA.NatSocket.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.NatSocket.Base
{
    internal abstract class Searcher
    {
        private readonly List<NatDevice> _devices = new List<NatDevice>();
        protected List<UdpClient> Sockets;
        public EventHandler<DeviceEventArgs> DeviceFound;
        internal DateTime NextSearch = DateTime.UtcNow;

        public async Task<IEnumerable<NatDevice>> Search(CancellationToken cancelationToken)
        {
            await Task.Factory.StartNew(async _ =>
            {
                NatDiscoverer.TraceSource.LogInfo("Searching for: {0}", GetType().Name);
                while (!cancelationToken.IsCancellationRequested)
                {
                    Discover(cancelationToken);
                    Receive(cancelationToken);
                }
                CloseSockets();
            }, cancelationToken);
            return _devices;
        }

        private void Discover(CancellationToken cancelationToken)
        {
            if (DateTime.UtcNow < NextSearch) return;

            foreach (var socket in Sockets)
            {
                try
                {
                    Discover(socket, cancelationToken);
                }
                catch (Exception e)
                {
                    NatDiscoverer.TraceSource.LogError("Error searching {0} - Details:", GetType().Name);
                    NatDiscoverer.TraceSource.LogError(e.ToString());
                }
            }
        }

        private void Receive(CancellationToken cancelationToken)
        {
            foreach (var client in Sockets.Where(x => x.Available > 0))
            {
                if (cancelationToken.IsCancellationRequested) return;

                var localHost = ((IPEndPoint)client.Client.LocalEndPoint).Address;
                var receivedFrom = new IPEndPoint(IPAddress.None, 0);
                var buffer = client.Receive(ref receivedFrom);
                var device = AnalyseReceivedResponse(localHost, buffer, receivedFrom);

                if (device != null) RaiseDeviceFound(device);
            }
        }


        protected abstract void Discover(UdpClient client, CancellationToken cancelationToken);

        public abstract NatDevice AnalyseReceivedResponse(IPAddress localAddress, byte[] response, IPEndPoint endpoint);

        public void CloseSockets()
        {
            foreach (var udpClient in Sockets)
            {
                udpClient.Close();
            }
        }

        private void RaiseDeviceFound(NatDevice device)
        {
            _devices.Add(device);
            var handler = DeviceFound;
            if (handler != null)
                handler(this, new DeviceEventArgs(device));
        }
    }
}
