/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V2.2.2.1
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
*版本号： V2.2.2.1
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Enums;
using SAEA.NatSocket.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SAEA.NatSocket.Base
{
    /// <summary>
	/// Represents a NAT device and provides access to the operation set that allows
	/// open (forward) ports, close ports and get the externa (visible) IP address.
	/// </summary>
	public abstract class NatDevice
    {
        private readonly HashSet<Mapping> _openedMapping = new HashSet<Mapping>();
        protected DateTime LastSeen { get; private set; }

        internal void Touch()
        {
            LastSeen = DateTime.Now;
        }

        /// <summary>
        /// Creates the port map asynchronous.
        /// </summary>
        /// <param name="mapping">The <see cref="Mapping">Mapping</see> entry.</param>
        /// <example>
        /// device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1700, 1600));
        /// </example>
        /// <exception cref="MappingException">MappingException</exception>
        public abstract Task CreatePortMapAsync(Mapping mapping);

        /// <summary>
        /// Deletes a mapped port asynchronous.
        /// </summary>
        /// <param name="mapping">The <see cref="Mapping">Mapping</see> entry.</param>
        /// <example>
        /// device.DeletePortMapAsync(new Mapping(Protocol.Tcp, 1700, 1600));
        /// </example>
        /// <exception cref="MappingException">MappingException-class</exception>
        public abstract Task DeletePortMapAsync(Mapping mapping);

        /// <summary>
        /// Gets all mappings asynchronous.
        /// </summary>
        /// <returns>
        /// The list of all forwarded ports
        /// </returns>
        /// <example>
        /// var mappings = await device.GetAllMappingsAsync();
        /// foreach(var mapping in mappings)
        /// {
        ///	 ConsoleHelper.WriteLine(mapping)
        /// }
        /// </example>
        /// <exception cref="MappingException">MappingException</exception>
        public abstract Task<IEnumerable<Mapping>> GetAllMappingsAsync();

        /// <summary>
        /// Gets the external (visible) IP address asynchronous. This is the NAT device IP address
        /// </summary>
        /// <returns>
        /// The public IP addrees
        /// </returns>
        /// <example>
        /// ConsoleHelper.WriteLine("My public IP is: {0}", await device.GetExternalIPAsync());
        /// </example>
        /// <exception cref="MappingException">MappingException</exception>
        public abstract Task<IPAddress> GetExternalIPAsync();

        /// <summary>
        /// Gets the specified mapping asynchronous.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <param name="port">The port.</param>
        /// <returns>
        /// The matching mapping
        /// </returns>
        public abstract Task<Mapping> GetSpecificMappingAsync(Protocol protocol, int port);

        protected void RegisterMapping(Mapping mapping)
        {
            _openedMapping.Remove(mapping);
            _openedMapping.Add(mapping);
        }

        protected void UnregisterMapping(Mapping mapping)
        {
            _openedMapping.RemoveWhere(x => x.Equals(mapping));
        }


        internal void ReleaseMapping(IEnumerable<Mapping> mappings)
        {
            var maparr = mappings.ToArray();
            var mapCount = maparr.Length;
            NatDiscoverer.TraceSource.LogInfo("{0} ports to close", mapCount);
            for (var i = 0; i < mapCount; i++)
            {
                var mapping = _openedMapping.ElementAt(i);

                try
                {
                    DeletePortMapAsync(mapping);
                    NatDiscoverer.TraceSource.LogInfo(mapping + " port successfully closed");
                }
                catch (Exception)
                {
                    NatDiscoverer.TraceSource.LogError(mapping + " port couldn't be close");
                }
            }
        }

        internal void ReleaseAll()
        {
            ReleaseMapping(_openedMapping);
        }

        internal void ReleaseSessionMappings()
        {
            var mappings = from m in _openedMapping
                           where m.LifetimeType == MappingLifetime.Session
                           select m;

            ReleaseMapping(mappings);
        }


        internal async Task RenewMappings()
        {
            var mappings = _openedMapping.Where(x => x.ShoundRenew());
            foreach (var mapping in mappings.ToArray())
            {
                var m = mapping;
                await RenewMapping(m);
            }
        }



        private async Task RenewMapping(Mapping mapping)
        {
            var renewMapping = new Mapping(mapping);
            try
            {
                renewMapping.Expiration = DateTime.UtcNow.AddSeconds(mapping.Lifetime);

                NatDiscoverer.TraceSource.LogInfo("Renewing mapping {0}", renewMapping);
                await CreatePortMapAsync(renewMapping);
                NatDiscoverer.TraceSource.LogInfo("Next renew scheduled at: {0}",
                                                  renewMapping.Expiration.ToLocalTime().TimeOfDay);
            }
            catch (Exception)
            {
                NatDiscoverer.TraceSource.LogWarn("Renew {0} failed", mapping);
            }
        }
    }
}
