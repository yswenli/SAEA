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
*命名空间：SAEA.RPC.Consumer
*文件名： ServiceConsumer
*版本号： v26.4.23.1
*唯一标识：70935e55-32c2-4bda-addc-e3cc705125ef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：ServiceConsumer接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:32
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.RPC.Model;
using SAEA.Sockets.Pipeline;
using System;
using System.Threading.Tasks;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// rpc消费者
    /// </summary>
    public class ServiceConsumer : IDisposable
    {
        ConsumerMultiplexer _consumerMultiplexer = null;

        Uri _uri;

        /// <summary>
        /// 推送模式下的数据通知事件
        /// </summary>
        public event OnNoticedHandler OnNoticed;

        /// <summary>
        /// 处理器链装配回调：每条连接在 RPC 解码之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器；服务端推送的通知也可在链上观察。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get { return _consumerMultiplexer.ConfigurePipeline; }
            set { _consumerMultiplexer.ConfigurePipeline = value; }
        }

        /// <summary>
        /// 是否与服务端保持可用连接
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _consumerMultiplexer.IsConnected;
            }
        }

        /// <summary>
        /// 以本机默认地址创建 rpc 消费者
        /// </summary>
        public ServiceConsumer() : this(new Uri("rpc://127.0.0.1:39654")) { }

        /// <summary>
        /// rpc消费者
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        /// <param name="retry"></param>
        /// <param name="timeOut"></param>
        public ServiceConsumer(Uri uri, int links = 4, int retry = 5, int timeOut = 10 * 1000)
        {
            _uri = uri;
            _consumerMultiplexer = ConsumerMultiplexer.Create(_uri, links, timeOut, retry);
            _consumerMultiplexer.NoticeHandler = _consumerMultiplexer_OnNoticed;
            _consumerMultiplexer.DisconnectedHandler = _consumerMultiplexer_OnDisconnected;
            _consumerMultiplexer.ErrorHandler = _consumerMultiplexer_OnError;
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public bool Reconnect()
        {
            return _consumerMultiplexer.Reconnect();
        }

        private void _consumerMultiplexer_OnDisconnected(string ID, Exception ex)
        {
            var message = ex == null ? "(无异常)" : ex.Message;
            ExceptionCollector.Add("Consumer", new RPCSocketException("ServiceConsumer Socket Disconnected:" + message, ex));
        }

        private void _consumerMultiplexer_OnError(string ID, Exception ex)
        {
            ExceptionCollector.Add("Consumer", new RPCSocketException("ServiceConsumer Socket Exception:" + ex.Message, ex));
        }


        private void _consumerMultiplexer_OnNoticed(byte[] serializeData)
        {
            OnNoticed?.Invoke(serializeData);
        }



        /// <summary>
        /// 调用远程RPC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public T RemoteCall<T>(string serviceName, string method, params object[] args)
        {
            T t = default(T);

            byte[] abytes = null;

            if (args != null)
            {
                abytes = SAEASerialize.Serialize(args);
            }

            var data = _consumerMultiplexer.Request(serviceName, method, abytes);

            if (data != null)
            {
                t = SAEASerialize.Deserialize<T>(data);
            }

            return t;
        }

        /// <summary>
        /// 推送模式
        /// </summary>
        public void RegistReceiveNotice()
        {
            _consumerMultiplexer.RegistReceiveNotice();
        }

        /// <summary>
        /// 断开连接并释放多路复用连接池
        /// </summary>
        public void Disconnect()
        {
            _consumerMultiplexer.Dispose();
        }

        /// <summary>
        /// 断开连接并释放资源
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}