/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Consumer
*文件名： ServiceConsumer
*版本号： V3.3.3.5
*唯一标识：aca3e22d-c8b2-402b-b1fd-77882c780532
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 15:14:36
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 15:14:36
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Serialize;
using System;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// rpc消费者
    /// </summary>
    public class ServiceConsumer
    {
        ConsumerMultiplexer _consumerMultiplexer = null;

        int _retry = 5;

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
            _retry = retry;
            _consumerMultiplexer = ConsumerMultiplexer.Create(uri, links, timeOut);
            _consumerMultiplexer.OnDisconnected += _consumerMultiplexer_OnDisconnected;
            _consumerMultiplexer.OnError += _consumerMultiplexer_OnError;
        }

        private void _consumerMultiplexer_OnDisconnected(string ID, Exception ex)
        {
            ExceptionCollector.Add("Consumer", new RPCSocketException("ServiceConsumer Socket Disconnected:" + ex.Message, ex));
        }

        private void _consumerMultiplexer_OnError(string ID, Exception ex)
        {
            ExceptionCollector.Add("Consumer", new RPCSocketException("ServiceConsumer Socket Exception:" + ex.Message, ex));
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
                abytes = ParamsSerializeUtil.Serialize(args);
            }

            var data = _consumerMultiplexer.Request(serviceName, method, abytes, _retry);

            if (data != null)
            {
                try
                {
                    int offset = 0;
                    t = (T)ParamsSerializeUtil.Deserialize(typeof(T), data, ref offset);
                }
                catch
                {

                }
            }
            return t;
        }
    }
}
