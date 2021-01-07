/****************************************************************************
*项目名称：SAEA.MVC
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC
*类 名 称：EventStream
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/6 14:02:09
*描述：
*=====================================================================
*修改时间：2021/1/6 14:02:09
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.Http.Model;
using System;
using System.Net;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// SSE服务器事件流
    /// </summary>
    public class EventStream : ActionResult, IEventStream
    {
        /// <summary>
        /// SSE服务器事件流
        /// </summary>
        /// <param name="retry">指定浏览器重新发起连接的时间间隔</param>
        public EventStream(int retry = 3 * 1000)
        {
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "text/event-stream; charset=utf-8";
            this.Status = HttpStatusCode.OK;
            HttpContext.Current.Response.ContentType = "text/event-stream; charset=utf-8";
            HttpContext.Current.Response.SetHeader(ResponseHeaderType.CacheControl, "no-cache");
            HttpContext.Current.Response.SetHeader(ResponseHeaderType.KeepAlive, "timeout=5");
            HttpContext.Current.Response.Status = HttpStatusCode.OK;
            HttpContext.Current.Response.SendHeader(-1);

            ServerSent(Encoding.UTF8.GetBytes($"retry: {retry}\n\n"));

            var pong = $"SAEAServer PONG {DateTimeHelper.Now:yyyy:MM:dd HH:mm:ss.fff}";

            TaskHelper.LongRunning(() =>
            {
                ServerSent(Encoding.UTF8.GetBytes($": {SerializeHelper.Serialize(pong)}\n\n"));
            }, 1000);
        }
        /// <summary>
        /// 发送通知
        /// </summary>
        /// <param name="str"></param>
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
            HttpContext.Current.Response.SendData(content);
        }
    }
}
