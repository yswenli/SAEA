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
*唯一标识：7eb38b60-db6b-416d-bd1f-78768773a98f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：IUserToken接口
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IUserToken接口
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
        /// 发送缓冲区的所有权对象。当发送数据为零拷贝时（调用方内存直接发送）为 null；
        /// 当库内从池中租用了缓冲区时，指向该池化对象，由发送完成回调负责释放。
        /// </summary>
        IDisposable SendingOwner { get; set; }

        /// <summary>
        /// 原子地取出并清空发送缓冲区所有权对象；取出后由调用方负责释放。
        /// 发送完成回调与断开清理可能并发，必须通过本方法保证恰好释放一次。
        /// </summary>
        IDisposable TakeSendingOwner();

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