/***************************************************************************** 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Interface
*文件名： IServerSokcet
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*****************************************************************************/

using SAEA.Sockets.Handler;

namespace SAEA.Sockets
{
    /// <summary>
    /// 服务器端
    /// </summary>
    public interface IServerSokcet
    {
        /// <summary>
        /// 配置项
        /// </summary>
        ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 建立连接事件
        /// </summary>
        event OnAcceptedHandler OnAccepted;

        /// <summary>
        /// 异常事件
        /// </summary>
        event OnErrorHandler OnError;

        /// <summary>
        /// 接收数据事件
        /// </summary>
        event OnReceiveHandler OnReceive;

        /// <summary>
        /// 客户端断开事件
        /// </summary>
        event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 启动
        /// </summary>
        /// <param name="backlog"></param>
        void Start(int backlog = 10 * 1000);

        /// <summary>
        /// 客户端连接数
        /// </summary>
        int ClientCounts { get; }

        /// <summary>
        /// 获取当前UserToken或Channel对象
        /// </summary>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        object GetCurrentObj(string sessionID);

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        void SendAsync(string sessionID, byte[] data);

        /// <summary>
        /// http end
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        void End(string sessionID, byte[] data);

        /// <summary>
        /// 停止
        /// </summary>
        void Stop();

        /// <summary>
        /// 断开指定会话
        /// </summary>
        /// <param name="obj"></param>
        void Disconnecte(object obj);
       
        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();
    }
}
