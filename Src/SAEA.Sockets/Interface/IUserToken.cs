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
*命名空间：SAEA.Sockets.Interface
*文件名： IUserToken
*版本号： v26.4.23.1
*唯一标识：1b1a94af-46c9-4760-b5d9-adb0d44acd82
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Net.Sockets;

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 连接信息类
    /// </summary>
    public interface IUserToken : ISession
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        string Guid { get; }

        /// <summary>
        /// 套接字对象
        /// </summary>
        Socket Socket
        {
            get; set;
        }

        /// <summary>
        /// 连接时间
        /// </summary>
        DateTime Linked
        {
            get; set;
        }

        /// <summary>
        /// 活动时间
        /// </summary>
        DateTime Actived
        {
            get; set;
        }

        /// <summary>
        /// 读取操作的SocketAsyncEventArgs对象
        /// </summary>
        SocketAsyncEventArgs ReadArgs
        {
            get; set;
        }

        /// <summary>
        /// 写入操作的SocketAsyncEventArgs对象
        /// </summary>
        SocketAsyncEventArgs WriteArgs
        {
            get; set;
        }

        /// <summary>
        /// 编码器对象
        /// </summary>
        ICoder Coder
        {
            get; set;
        }

        /// <summary>
        /// 等待写入操作完成
        /// </summary>
        /// <param name="timeOut">超时时间</param>
        /// <returns>是否成功</returns>
        bool WaitWrite(int timeOut);

        void ReleaseWrite();

        bool IsSending { get; set; }

        void Clear();
    }
}