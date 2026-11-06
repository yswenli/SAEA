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
*命名空间：SAEA.MQTT.Adapter
*文件名： IMqttServerAdapter
*版本号： v26.4.23.1
*唯一标识：8a31d3ad-ae1e-4f34-8e57-ca45b9bb8199
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerAdapter适配器类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Adapter
{
    /// <summary>
    /// 服务端监听适配器，负责接受连接并把每个连接交给客户端处理器
    /// </summary>
    public interface IMqttServerAdapter : IDisposable
    {
        /// <summary>
        /// 新连接接入时的处理回调，参数为已完成握手的信道适配器
        /// </summary>
        Func<IMqttChannelAdapter, Task> ClientHandler { get; set; }

        /// <summary>
        /// 按服务端选项开始监听
        /// </summary>
        /// <param name="options">服务端选项，包含监听地址与安全设置</param>
        /// <returns>开始监听完成的异步任务</returns>
        Task StartAsync(IMqttServerOptions options);
        /// <summary>
        /// 停止监听并释放已接受的连接
        /// </summary>
        /// <returns>停止完成的异步任务</returns>
        Task StopAsync();
    }
}
