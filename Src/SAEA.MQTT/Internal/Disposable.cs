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
*命名空间：SAEA.MQTT.Internal
*文件名： Disposable
*版本号： v26.4.23.1
*唯一标识：81b1d816-d336-41c3-9935-dc26188d3e60
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：Disposable接口
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

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 统一管理释放状态的抽象基类
    /// </summary>
    public abstract class Disposable : IDisposable
    {
        /// <summary>
        /// 是否已释放
        /// </summary>
        protected bool IsDisposed { get; private set; }

        /// <summary>
        /// 已释放时抛出 ObjectDisposedException
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 释放派生类的资源，基类实现为空
        /// </summary>
        /// <param name="disposing">为 true 表示由 Dispose 显式调用而非终结器回收</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// 释放对象，重复调用无副作用
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.

            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
