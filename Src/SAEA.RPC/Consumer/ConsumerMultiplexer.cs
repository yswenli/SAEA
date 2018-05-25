/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Consumer
*文件名： ConsumerMultiplexer
*版本号： V1.0.0.0
*唯一标识：85b40df2-6436-4a63-8358-6a0ed32b20cd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/25 16:14:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/25 16:14:32
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Handler;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// 使用多路复用概念来实现高效率连接
    /// </summary>
    public class ConsumerMultiplexer
    {
        static HashMap<string, int, RClient> _hashMap = new HashMap<string, int, RClient>();

        static object _locker = new object();

        /// <summary>
        /// 创建多路复用
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        public static void Create(Uri uri, int links = 4)
        {
            lock (_locker)
            {
                if (!_hashMap.Exits(uri.ToString()))
                {
                    for (int i = 0; i < links; i++)
                    {
                        var rClient = new RClient(uri);
                        rClient.OnDisconnected += RClient_OnDisconnected;
                        rClient.OnError += RClient_OnError;
                        rClient.OnMsg += RClient_OnMsg;
                        if (!rClient.Connect())
                        {
                            throw new RPCSocketException($"连接到{uri.ToString()}失败");
                        }
                        _hashMap.Set(uri.ToString(), i, rClient);
                    }
                }
            }
        }

        

        #region MyRegion

        public static event OnErrorHandler OnError;

        public static event OnDisconnectedHandler OnDisconnected;

        public static event Action<RSocketMsg> OnMsg;


        private static void RClient_OnMsg(RSocketMsg obj)
        {
            OnMsg?.Invoke(obj);
        }

        private static void RClient_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private static void RClient_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }
        #endregion
    }
}
