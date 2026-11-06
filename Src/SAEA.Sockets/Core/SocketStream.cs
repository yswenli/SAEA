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
*命名空间：SAEA.Sockets.Core
*文件名： SocketStream
*版本号： v26.4.23.1
*唯一标识：dd4ffcae-1357-46ec-bb59-e880cf11d86b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/10/24 23:36:24
*描述：SocketStream流类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Caching;

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

        BlockingQueue<byte[]> _queue;

        List<byte> _list;

        int _length = 0;

        /// <summary>
        /// 套节字流
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ownsClient"></param>
        public SocketStream(IClientSocket client, bool ownsClient = true)
        {
            _client = client;
            _ownsClient = ownsClient;
            _client.OnClientReceiveSpan += _client_OnReceiveSpan;
            _queue = new BlockingQueue<byte[]>();
            _list = new List<byte>();
        }

        private void _client_OnReceiveSpan(ReadOnlySpan<byte> data)
        {
            var arr = data.ToArray();
            _queue.Enqueue(arr);
            Interlocked.Add(ref _length, arr.Length);
        }

        /// <summary>
        /// 是否可读，跟随连接状态
        /// </summary>
        public override bool CanRead => _client.Connected;

        /// <summary>
        /// 是否可定位，跟随连接状态；实际定位操作未实现
        /// </summary>
        public override bool CanSeek => _client.Connected;

        /// <summary>
        /// 是否可写，跟随连接状态
        /// </summary>
        public override bool CanWrite => _client.Connected;

        /// <summary>
        /// 已接收但尚未读取的字节数
        /// </summary>
        public override long Length => _length;

        /// <summary>
        /// 当前读写位置，不支持，访问即抛 NotImplementedException
        /// </summary>
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// 刷新缓冲，不支持，调用即抛 NotImplementedException
        /// </summary>
        /// <exception cref="NotImplementedException">该流不支持刷新</exception>
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
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            var data = _queue.Dequeue(_client.SocketOption.ActionTimeout);
            if (data != null && data.Length > 0)
            {
                _list.AddRange(data);
            }

            var take = Math.Min(count, _list.Count);
            if (take <= 0) return 0;

            _list.CopyTo(0, buffer, offset, take);
            _list.RemoveRange(0, take);
            Interlocked.Add(ref _length, 0 - take);
            return take;
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

        /// <summary>
        /// 定位，不支持，调用即抛 NotImplementedException
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <param name="origin">定位基准</param>
        /// <returns>定位后的位置</returns>
        /// <exception cref="NotImplementedException">该流不支持定位</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 设置流长度，不支持，调用即抛 NotImplementedException
        /// </summary>
        /// <param name="value">目标长度</param>
        /// <exception cref="NotImplementedException">该流不支持修改长度</exception>
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
            _client.SendAsync(new ReadOnlyMemory<byte>(data));
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