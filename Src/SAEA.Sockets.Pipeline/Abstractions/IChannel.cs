/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： IChannel
*版本号： v26.11.5.1
*唯一标识：2acdd2fb-1def-4a37-a0b9-3f5983ce455d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：一条连接的应用级视图：标识、活跃状态、连接级属性和关闭入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：一条连接的应用级视图：标识、活跃状态、连接级属性和关闭入口
*
*****************************************************************************/
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 一条连接的应用级视图：标识、活跃状态、连接级属性和关闭入口。
    /// 随连接创建、随连接关闭清空，不跨连接复用。
    /// </summary>
    public interface IChannel
    {
        /// <summary>连接标识。</summary>
        string Id { get; }

        /// <summary>连接是否可继续收发。</summary>
        bool IsActive { get; }

        /// <summary>
        /// 当前连接的属性快照。底层实现必须线程安全，因为同一连接的多条消息并发执行。
        /// 只应用于连接级状态，禁止放入跨连接共享数据。
        /// </summary>
        IReadOnlyDictionary<string, object> Attributes { get; }

        /// <summary>写入连接级属性。</summary>
        void SetAttribute(string key, object value);

        /// <summary>读取连接级属性；未设置返回 null。</summary>
        object GetAttribute(string key);

        /// <summary>移除连接级属性；键不存在返回 false。</summary>
        bool TryRemoveAttribute(string key, out object value);

        /// <summary>
        /// 请求关闭连接，幂等。返回只表示已进入收口流程，不表示资源回收完成；
        /// 收口在后台继续执行，不会等待调用方自身。
        /// </summary>
        Task CloseAsync(string reason);
    }
}