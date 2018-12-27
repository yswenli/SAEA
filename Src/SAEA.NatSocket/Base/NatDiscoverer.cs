/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V3.6.2.1
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
*版本号： V3.6.2.1
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Exceptions;
using SAEA.NatSocket.Pmp;
using SAEA.NatSocket.Upnp;
using SAEA.NatSocket.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.NatSocket.Base
{
    public class NatDiscoverer
    {

        public readonly static TraceSource TraceSource = new TraceSource("SAEA.NatSocket");

        private static readonly Dictionary<string, NatDevice> Devices = new Dictionary<string, NatDevice>();

        // Finalizer is never used however its destructor, that releases the open ports, is invoked by the
        // process as part of the shuting down step. So, don't remove it!
        private static readonly Finalizer Finalizer = new Finalizer();
        internal static readonly Timer RenewTimer = new Timer(RenewMappings, null, 5000, 2000);

        /// <summary>
        /// Discovers and returns an UPnp or Pmp NAT device; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see>
        /// exception is thrown after 3 seconds. 
        /// </summary>
        /// <returns>A NAT device</returns>
        /// <exception cref="NatDeviceNotFoundException">when no NAT found before 3 seconds.</exception>
        public async Task<NatDevice> DiscoverDeviceAsync()
        {
            var cts = new CancellationTokenSource(3 * 1000);
            return await DiscoverDeviceAsync(PortMapper.Pmp | PortMapper.Upnp, cts);
        }

        /// <summary>
        /// Discovers and returns a NAT device for the specified type; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see> 
        /// exception is thrown when it is cancelled. 
        /// </summary>
        /// <remarks>
        /// It allows to specify the NAT type to discover as well as the cancellation token in order.
        /// </remarks>
        /// <param name="portMapper">Port mapper protocol; Upnp, Pmp or both</param>
        /// <param name="cancellationTokenSource">Cancellation token source for cancelling the discovery process</param>
        /// <returns>A NAT device</returns>
        /// <exception cref="NatDeviceNotFoundException">when no NAT found before cancellation</exception>
        public async Task<NatDevice> DiscoverDeviceAsync(PortMapper portMapper, CancellationTokenSource cancellationTokenSource)
        {
            Guard.IsTrue(portMapper.HasFlag(PortMapper.Upnp) || portMapper.HasFlag(PortMapper.Pmp), "portMapper");
            Guard.IsNotNull(cancellationTokenSource, "cancellationTokenSource");

            var devices = await DiscoverAsync(portMapper, true, cancellationTokenSource);
            var device = devices.FirstOrDefault();
            if (device == null)
            {
                TraceSource.LogInfo("Device not found. Common reasons:");
                TraceSource.LogInfo("\t* No device is present or,");
                TraceSource.LogInfo("\t* Upnp is disabled in the router or");
                TraceSource.LogInfo("\t* Antivirus software is filtering SSDP (discovery protocol).");
                throw new NatDeviceNotFoundException();
            }
            return device;
        }

        /// <summary>
        /// Discovers and returns all NAT devices for the specified type. If no NAT device is found it returns an empty enumerable
        /// </summary>
        /// <param name="portMapper">Port mapper protocol; Upnp, Pmp or both</param>
        /// <param name="cancellationTokenSource">Cancellation token source for cancelling the discovery process</param>
        /// <returns>All found NAT devices</returns>

        public async Task<IEnumerable<NatDevice>> DiscoverDevicesAsync(PortMapper portMapper, CancellationTokenSource cancellationTokenSource)
        {
            Guard.IsTrue(portMapper.HasFlag(PortMapper.Upnp) || portMapper.HasFlag(PortMapper.Pmp), "portMapper");
            Guard.IsNotNull(cancellationTokenSource, "cancellationTokenSource");

            var devices = await DiscoverAsync(portMapper, false, cancellationTokenSource);
            return devices.ToArray();
        }

        private async Task<IEnumerable<NatDevice>> DiscoverAsync(PortMapper portMapper, bool onlyOne, CancellationTokenSource cts)
        {
            TraceSource.LogInfo("Start Discovery");
            var searcherTasks = new List<Task<IEnumerable<NatDevice>>>();
            if (portMapper.HasFlag(PortMapper.Upnp))
            {
                var upnpSearcher = new UpnpSearcher(new IPAddressesProvider());
                upnpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
                searcherTasks.Add(upnpSearcher.Search(cts.Token));
            }
            if (portMapper.HasFlag(PortMapper.Pmp))
            {
                var pmpSearcher = new PmpSearcher(new IPAddressesProvider());
                pmpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
                searcherTasks.Add(pmpSearcher.Search(cts.Token));
            }

            await Task.WhenAll(searcherTasks);
            TraceSource.LogInfo("Stop Discovery");

            var devices = searcherTasks.SelectMany(x => x.Result);
            foreach (var device in devices)
            {
                var key = device.ToString();
                NatDevice nat;
                if (Devices.TryGetValue(key, out nat))
                {
                    nat.Touch();
                }
                else
                {
                    Devices.Add(key, device);
                }
            }
            return devices;
        }


        /// <summary>
        /// Release all ports opened by SAEA.NatSocket. 
        /// </summary>
        /// <remarks>
        /// If ReleaseOnShutdown value is true, it release all the mappings created through the library.
        /// </remarks>
        public static void ReleaseAll()
        {
            foreach (var device in Devices.Values)
            {
                device.ReleaseAll();
            }
        }

        internal static void ReleaseSessionMappings()
        {
            foreach (var device in Devices.Values)
            {
                device.ReleaseSessionMappings();
            }
        }

        private static void RenewMappings(object state)
        {

            Task.Factory.StartNew(async () =>
            {
                foreach (var device in Devices.Values)
                {
                    await device.RenewMappings();
                }
            });
        }
    }
}
