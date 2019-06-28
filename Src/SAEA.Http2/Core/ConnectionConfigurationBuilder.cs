/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ConnectionConfigurationBuilder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 13:44:55
*描述：
*=====================================================================
*修改时间：2019/6/28 13:44:55
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
    /// 连接配置的生成器
    /// </summary>
    public class ConnectionConfigurationBuilder
    {
        private const int DefaultClientPrefaceTimeout = 1000;

        bool isServer = true;
        Func<IStream, bool> streamListener = null;
        HuffmanStrategy? huffmanStrategy = null;
        Settings settings = Settings.Default;
        ArrayPool<byte> bufferPool = null;
        int clientPrefaceTimeout = DefaultClientPrefaceTimeout;

        /// <summary>
        /// 为连接配置创建新的生成器
        /// </summary>
        /// <param name="isServer"></param>
        public ConnectionConfigurationBuilder(bool isServer)
        {
            this.isServer = isServer;
        }

        /// <summary>
        /// 从配置连接设置中建立连接配置
        /// </summary>
        public ConnectionConfiguration Build()
        {
            if (isServer && streamListener == null)
            {
                throw new Exception("服务器连接必须已配置StreamListener");
            }

            var pool = bufferPool;
            if (pool == null) pool = ArrayPool<byte>.Shared;

            var config = new ConnectionConfiguration(
                isServer: isServer,
                streamListener: streamListener,
                huffmanStrategy: huffmanStrategy,
                settings: settings,
                bufferPool: pool,
                clientPrefaceTimeout: clientPrefaceTimeout);

            return config;
        }

        /// <summary>
        /// 配置远程对等方打开新流时应调用的函数
        /// </summary>
        public ConnectionConfigurationBuilder UseStreamListener(
            Func<IStream, bool> streamListener)
        {
            this.streamListener = streamListener;
            return this;
        }

        /// <summary>
        /// 配置对传出头应用Huffman编码的策略。
        /// </summary>
        public ConnectionConfigurationBuilder UseHuffmanStrategy(HuffmanStrategy strategy)
        {
            this.huffmanStrategy = strategy;
            return this;
        }

        /// <summary>
        /// 允许覆盖连接的HTTP/2设置
        /// </summary>
        public ConnectionConfigurationBuilder UseSettings(Settings settings)
        {
            if (!settings.Valid)
            {
                throw new Exception("无效设置");
            }
            this.settings = settings;
            return this;
        }

        /// <summary>
        ///配置将用于分配发送和接收缓冲区的缓冲池。
        /// </summary>
        /// <param name="pool"></param>
        public ConnectionConfigurationBuilder UseBufferPool(ArrayPool<byte> pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            this.bufferPool = pool;
            return this;
        }

        /// <summary>
        /// 允许覆盖在服务器端启动时等待客户端前言的时间。
        /// </summary>
        /// <param name="timeout"></param>
        public ConnectionConfigurationBuilder UseClientPrefaceTimeout(int timeout)
        {
            if (timeout <= 0) throw new ArgumentException(nameof(timeout));
            this.clientPrefaceTimeout = timeout;
            return this;
        }
    }
}
