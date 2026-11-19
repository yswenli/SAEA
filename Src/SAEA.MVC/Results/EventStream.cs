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
*命名空间：SAEA.MVC.Results
*文件名： EventStream
*版本号： v26.4.23.1
*唯一标识：48b0010e-7dc1-4a5f-ba80-f0f36b0a913f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/06 19:27:25
*描述：EventStream流类
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:26:46
*修改人： yswenli
*版本号： v26.11.16.1
*描述：命名空间改为与目录一致（SAEA.MVC.Attributes/Controllers/Hosting/Results）
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.MVC.Hosting;
using SAEA.Http.Model;

using System.Net;
using System.Text;
using System.Threading;

namespace SAEA.MVC.Results
{
    /// <summary>
    /// SSE服务器事件流
    /// </summary>
    public class EventStream : ActionResult, IEventStream
    {
        /// <summary>
        /// 最后一次接收到的事件的标识符
        /// </summary>
        public int LastEventID
        {
            get;
            private set;
        }

        /// <summary>
        /// 心跳间隔，单位毫秒
        /// </summary>
        private const int HeartbeatInterval = 1000;

        /// <summary>
        /// 心跳的取消源，连接失效后据此结束心跳线程
        /// </summary>
        private readonly CancellationTokenSource _heartbeatCts = new CancellationTokenSource();

        /// <summary>
        /// SSE服务器事件流
        /// </summary>
        /// <param name="retry">指定浏览器重新发起连接的时间间隔</param>
        public EventStream(int retry = 3 * 1000)
        {
            this.ContentEncoding = Encoding.UTF8;

            if (HttpContext.Current.Request.Headers.ContainsKey("Last-Event-ID"))
            {
                if (int.TryParse(HttpContext.Current.Request.Headers["Last-Event-ID"], out int id))
                {
                    LastEventID = id;
                }
            }

            HttpContext.Current.Response.ContentType = "text/event-stream; charset=utf-8";
            HttpContext.Current.Response.SetHeader(ResponseHeaderType.CacheControl, "no-cache");
            HttpContext.Current.Response.SetHeader(ResponseHeaderType.KeepAlive, "timeout=7200");
            HttpContext.Current.Response.Status = HttpStatusCode.OK;
            HttpContext.Current.Response.SendHeader(-1);

            // 报头已写出，后续发生异常不能再改状态码
            (HttpContext.Current as HttpContext)?.MarkResponseStarted();

            //心跳
            var pong = $"SAEAServer PONG {DateTimeHelper.Now:yyyy:MM:dd HH:mm:ss.fff}";

            var token = _heartbeatCts.Token;

            TaskHelper.LongRunning(() =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(HeartbeatInterval);

                        if (token.IsCancellationRequested) break;

                        ServerSent(Encoding.UTF8.GetBytes($": {SerializeHelper.Serialize(pong)}\n\n"));
                    }
                }
                catch
                {
                    // 客户端断开或连接已释放时写出会抛异常：结束心跳循环，避免线程跟着连接永久驻留
                    StopHeartbeat();
                }
            });

            ServerSent(Encoding.UTF8.GetBytes($"retry: {retry}\n\n"));
        }
        /// <summary>
        /// 发送通知
        /// </summary>
        /// <param name="t"></param>
        /// <param name="event"></param>
        /// <param name="id"></param>
        public void ServerSent<T>(T t, string @event = "message", string id = "") where T : class
        {
            if (t != null)
                ServerSent(Encoding.UTF8.GetBytes($"id: {id?.Trim()}\nevent: {@event?.Trim()}\ndata: {SerializeHelper.Serialize(t)}\n\n"));
        }
        /// <summary>
        /// 发送通知
        /// </summary>
        /// <param name="content"></param>
        public void ServerSent(byte[] content)
        {
            using var locker = ObjectLock.Create($"EventStream.ServerSent{HttpContext.Current.Request.UserHostAddress}");
            HttpContext.Current.Response.SendData(content);
        }

        /// <summary>
        /// 停止心跳循环，可重复调用
        /// </summary>
        private void StopHeartbeat()
        {
            _heartbeatCts.Cancel();
        }
    }
}
