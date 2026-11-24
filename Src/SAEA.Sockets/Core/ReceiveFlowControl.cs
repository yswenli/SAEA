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
*命名空间：SAEA.Sockets.Core
*文件名： ReceiveFlowControl
*版本号： v26.11.5.1
*唯一标识：c4b87065-f3c3-42ca-a4c8-c5fe636262db
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：接收流量控制的连接适配
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：v26.11.24.2
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 把一条连接的接收闸门适配为 <see cref="IReceiveFlowControl"/>。
    /// </summary>
    internal sealed class ReceiveFlowControl : IReceiveFlowControl, IGracefulCloseFlow
    {
        private readonly SaeaConnection _connection;

        private readonly Action _onClose;

        internal ReceiveFlowControl(SaeaConnection connection, Action onClose)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _onClose = onClose;
        }

        /// <summary>
        /// 是否已暂停读取。只反映应用级暂停，Pipe 背压由传输层自行处理。
        /// </summary>
        public bool IsPaused
        {
            get { return _connection.IsApplicationPaused; }
        }

        /// <summary>
        /// 暂停读取。幂等。
        /// </summary>
        public void Pause()
        {
            _connection.PauseApplication();
        }

        /// <summary>
        /// 恢复读取。幂等。
        /// </summary>
        public void Resume()
        {
            _connection.ResumeApplication();
        }

        /// <summary>
        /// 关闭该连接。幂等：连接已关闭时关闭回调不产生副作用。
        /// </summary>
        /// <param name="reason">关闭原因</param>
        public Task CloseAsync(string reason)
        {
            try { _onClose?.Invoke(); } catch { }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 半关发送方向，按字节上限与超时排空入站后关闭连接。
        /// 连接已释放或没有连接载体时退化为普通关闭，避免收口悬挂。
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数</param>
        /// <param name="timeoutMs">排空超时毫秒数</param>
        /// <returns>连接真正关闭后完成的任务</returns>
        public Task DrainAndCloseAsync(long drainBytes, int timeoutMs)
        {
            var connection = _connection;
            if (connection == null || connection.IsClosed)
            {
                try { _onClose?.Invoke(); } catch { }
                return Task.CompletedTask;
            }

            return connection.DrainAndCloseAsync(drainBytes, timeoutMs, _onClose);
        }
    }
}