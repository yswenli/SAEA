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
*命名空间：SAEA.MVC
*文件名： EventStream
*版本号： v26.4.23.1
*唯一标识：e9c86951-7688-4c93-b3fe-6c0eb4e461db
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/06 19:27:25
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/01/06 19:27:25
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.Http.Model;

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
        /// 最后一次接收到的事件的标识符
        /// </summary>
        public int LastEventID
        {
            get;
            private set;
        }

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

            //心跳
            var pong = $"SAEAServer PONG {DateTimeHelper.Now:yyyy:MM:dd HH:mm:ss.fff}";

            TaskHelper.LongRunning(() =>
            {
                ServerSent(Encoding.UTF8.GetBytes($": {SerializeHelper.Serialize(pong)}\n\n"));
            }, 1000);

            ServerSent(Encoding.UTF8.GetBytes($"retry: {retry}\n\n"));
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
            using var locker = ObjectLock.Create($"EventStream.ServerSent{HttpContext.Current.Request.UserHostAddress}");
            HttpContext.Current.Response.SendData(content);
        }
    }
}
