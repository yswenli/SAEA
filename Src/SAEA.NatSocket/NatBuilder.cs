/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V2.2.1.1
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
*版本号： V2.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Base;
using SAEA.NatSocket.Enums;
using System.Threading;

namespace SAEA.NatSocket
{
    /// <summary>
    /// Nat映射类
    /// </summary>
    public static class NatBuilder
    {
        /// <summary>
        /// 添加一个Nat映射
        /// </summary>
        /// <param name="isTcp"></param>
        /// <param name="isUPnP"></param>
        /// <param name="privatePort"></param>
        /// <param name="publicPort"></param>
        /// <param name="des"></param>
        /// <param name="timeOut"></param>
        public static async void Create(bool isTcp = true, bool isUPnP = true, int privatePort = 39654, int publicPort = 39654, string des = "SAEA.NAT", int timeOut = 10 * 1000)
        {
            var discoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(timeOut);
            var device = await discoverer.DiscoverDeviceAsync(isUPnP ? PortMapper.Upnp : PortMapper.Pmp, cts);
            await device.CreatePortMapAsync(new Mapping(isTcp ? Protocol.Tcp : Protocol.Udp, privatePort, publicPort, des));
        }

        /// <summary>
        /// 删除一个Nat映射
        /// </summary>
        /// <param name="isTcp"></param>
        /// <param name="isUPnP"></param>
        /// <param name="privatePort"></param>
        /// <param name="publicPort"></param>
        /// <param name="des"></param>
        /// <param name="timeOut"></param>
        public static async void Delete(bool isTcp = true, bool isUPnP = true, int privatePort = 39654, int publicPort = 39654, string des = "SAEA.NAT", int timeOut = 10 * 1000)
        {
            var discoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(timeOut);
            var device = await discoverer.DiscoverDeviceAsync(isUPnP ? PortMapper.Upnp : PortMapper.Pmp, cts);
            await device.DeletePortMapAsync(new Mapping(isTcp ? Protocol.Tcp : Protocol.Udp, privatePort, publicPort, des));
        }


    }
}
