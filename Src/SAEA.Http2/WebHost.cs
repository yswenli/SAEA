/****************************************************************************
*项目名称：SAEA.Http2
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2
*类 名 称：WebHost
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 15:50:12
*描述：
*=====================================================================
*修改时间：2019/6/28 15:50:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Net;

namespace SAEA.Http2
{
    /// <summary>
    /// web服务器
    /// </summary>
    public class WebHost
    {
        Http2Server _http2Server;

        public WebHost(IHttp2Invoker invoker = null, int port = 39654, int count = 100, int size = 1024, int timeOut = 180 * 1000)
        {
            _http2Server = new Http2Server(invoker, port, count, size, timeOut);
        }

        public void Start()
        {
            _http2Server.Start();
        }

        public void Stop()
        {
            _http2Server.Stop();
        }
    }
}
