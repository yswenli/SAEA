/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ConnectionConfiguration
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 17:21:55
*描述：
*=====================================================================
*修改时间：2019/6/27 17:21:55
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Buffers;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 连接的配置选项
    /// </summary>
    public class ConnectionConfiguration
    {
        public readonly bool IsServer;

        public readonly Func<IStream, bool> StreamListener;

        public readonly HuffmanStrategy? HuffmanStrategy;

        public readonly int ClientPrefaceTimeout;

        public readonly Settings Settings;

        public readonly ArrayPool<byte> BufferPool;

        internal ConnectionConfiguration(
            bool isServer,
            Func<IStream, bool> streamListener,
            HuffmanStrategy? huffmanStrategy,
            Settings settings,
            ArrayPool<byte> bufferPool,
            int clientPrefaceTimeout)
        {
            this.IsServer = isServer;
            this.StreamListener = streamListener;
            this.HuffmanStrategy = huffmanStrategy;
            this.Settings = settings;
            this.BufferPool = bufferPool;
            this.ClientPrefaceTimeout = clientPrefaceTimeout;
        }
    }
}
