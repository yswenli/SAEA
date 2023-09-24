/****************************************************************************
*项目名称：SAEA.Sockets.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Sockets.Core
*类 名 称：SocketKeeper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/24 15:38:17
*描述：
*=====================================================================
*修改时间：2019/5/24 15:38:17
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 从底层维持socket
    /// </summary>
    public static class SocketKeeper
    {
        /// <summary>
        /// 从底层维持socket连接，
        /// 主要用于tcp
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="keepAliveTime"></param>
        /// <param name="timeOut"></param>
        public static void KeepAlive(this Socket socket, int keepAliveTime = 180 * 1000, int timeOut = 5 * 1000)
        {
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);//是否启用Keep-Alive
            BitConverter.GetBytes((uint)keepAliveTime).CopyTo(inOptionValues, Marshal.SizeOf(dummy));//多长时间开始第一次探测
            BitConverter.GetBytes((uint)timeOut).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);//探测时间间隔

            try
            {
                socket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
            }
            catch (PlatformNotSupportedException)
            {
                try
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
                catch (PlatformNotSupportedException)
                {

                }
            }

        }
    }
}
