/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： SocketStream
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2016/10/26 15:54:21
*描述：
*
*=====================================================================
*修改标记
*创建时间：2016/10/26 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 套节字流
    /// </summary>
    public class SocketStream : Stream, IDisposable
    {
        bool _ownsClient = true;

        IClientSocket _client;

        List<byte> _cache = new List<byte>();

        object _readLocker = new object();

        int _avaliable = 0;

        AutoResetEvent _event = new AutoResetEvent(true);

        /// <summary>
        /// 套节字流
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ownsClient"></param>
        public SocketStream(IClientSocket client, bool ownsClient = true)
        {
            _client = client;
            _ownsClient = ownsClient;
            _client.OnReceive += _client_OnReceive;
        }

        private void _client_OnReceive(byte[] data)
        {
            lock (_readLocker)
            {
                _cache.AddRange(data);
            }
            if (Interlocked.Exchange(ref _avaliable, 1) == 0)
            {
                _event.Set();
            }

        }

        public override bool CanRead => _client.Connected;

        public override bool CanSeek => _client.Connected;

        public override bool CanWrite => _client.Connected;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 读取
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_readLocker)
            {
                if (_event.WaitOne(_client.SocketOption.TimeOut))
                {
                    var data = _cache.Skip(offset).Take(count).ToArray();
                    var length = data.Length;
                    Buffer.BlockCopy(data, 0, buffer, 0, length);
                    Interlocked.Exchange(ref _avaliable, 0);
                    return length;
                }
                else
                    throw new TimeoutException("读取数据超时");
            }
        }
        /// <summary>
        /// 读取
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public new async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            return await Task.Run(() => Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            var data = buffer.AsSpan().Slice(offset, count).ToArray();
            _client.SendAsync(data);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public new async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            await Task.Run(() => Write(buffer, offset, count));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public new void Dispose()
        {
            if (_ownsClient)
                _client.Dispose();
            base.Dispose();
        }
    }
}
