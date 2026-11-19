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
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：HEAD 请求在启动心跳前只回报头并收尾，避免心跳线程因正文被抑制而永久驻留
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
            HttpContext.Current.Response.Status = HttpStatusCode.OK;

            var response = HttpContext.Current.Response;

            // HEAD 指向无限流：SSE 给不出与 GET 等价的 Content-Length，只回报头后立即收尾。
            // 这里必须在 SendHeader 与心跳之前返回：心跳线程一旦起来就只剩异常能停它，而抑制正文时
            // SendData 是静默返回、不会抛异常，线程会跟着连接永久驻留
            if (response.SuppressBody)
            {
                try
                {
                    // bodyLen 传 0 表示没有正文：不写 Content-Length，Connection 按短连接写 close，与随后的 SendEnd 一致
                    response.SendHeader(0);

                    // 报头已写出，后续发生异常不能再改状态码
                    (HttpContext.Current as HttpContext)?.MarkResponseStarted();
                }
                finally
                {
                    // 心跳没有启动，取消源没有其它持有者，随本次响应释放；即使写报头失败也要断开连接
                    _heartbeatCts.Dispose();

                    response.SendEnd();
                }

                return;
            }

            // 心跳保活只属于长连接流：HEAD 已按短连接收尾，此时再写 Keep-Alive 会与 Connection: close 矛盾
            response.SetHeader(ResponseHeaderType.KeepAlive, "timeout=7200");

            response.SendHeader(-1);

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
