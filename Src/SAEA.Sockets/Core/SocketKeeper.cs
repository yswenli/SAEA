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
*命名空间：SAEA.Sockets.Core
*文件名： SocketKeeper
*版本号： v26.4.23.1
*唯一标识：d86d1005-f51a-465d-9cc4-b2fce6ea8fa5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/05/24 16:36:26
*描述：SocketKeeper类
*
*=====================================================================
*修改标记
*修改时间：2019/05/24 16:36:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：SocketKeeper类
*
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
