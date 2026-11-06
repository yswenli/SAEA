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
*命名空间：SAEA.Http.Model
*文件名： IWebHost
*版本号： v26.4.23.1
*唯一标识：19746f8a-94cc-4004-9c64-2afc80d67f33
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：IWebHost接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Http.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Model
{

    /// <summary>
    /// Web 主机定义，负责启停服务器并在连接上收发数据
    /// </summary>
    public interface IWebHost
    {
        /// <summary>
        /// 服务器是否正在运行
        /// </summary>
        bool IsRunning { get; set; }

        /// <summary>
        /// Web 服务器配置
        /// </summary>
        WebConfig WebConfig { get; set; }

        /// <summary>
        /// Http 服务器实用工具
        /// </summary>
        HttpUtility HttpUtility { get; }

        /// <summary>
        /// 路由附加参数，由业务自行解释
        /// </summary>
        object RouteParam { get; set; }

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        event ExceptionHandler OnException;

        /// <summary>
        /// 启动服务器
        /// </summary>
        void Start();

        /// <summary>
        /// 向指定连接发送数据，不关闭连接
        /// </summary>
        /// <param name="userToken">客户端连接令牌</param>
        /// <param name="data">待发送字节</param>
        void Send(IUserToken userToken, byte[] data);

        /// <summary>
        /// 断开指定连接
        /// </summary>
        /// <param name="userToken">客户端连接令牌</param>
        void Disconnect(IUserToken userToken);

        /// <summary>
        /// 发送数据后结束该连接
        /// </summary>
        /// <param name="userToken">客户端连接令牌</param>
        /// <param name="data">待发送字节</param>
        void End(IUserToken userToken, byte[] data);

        /// <summary>
        /// 停止服务器
        /// </summary>
        void Stop();
    }
}