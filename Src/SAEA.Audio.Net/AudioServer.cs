/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio
*类 名 称：AudioServer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Net;

using SAEA.Audio.Model;
using SAEA.Audio.Net;

namespace SAEA.Audio
{
    /// <summary>
    /// 语音服务器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AudioServer<T> where T : IStorage
    {
        TransferServer<T> _audioServer;

        public event Action<DataInfo> OnReceive;

        /// <summary>
        /// 语音服务器
        /// </summary>
        /// <param name="endPoint"></param>
        public AudioServer(IPEndPoint endPoint)
        {
            _audioServer = new TransferServer<T>(endPoint);

            _audioServer.OnReceive += (data) => OnReceive?.Invoke(data);
        }

        /// <summary>
        /// 语音服务器
        /// </summary>
        /// <param name="port"></param>
        public AudioServer(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {

        }

        /// <summary>
        /// 启动
        /// </summary>
        public void Start()
        {
            _audioServer.Start();
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            _audioServer.Stop();
        }
    }
}
