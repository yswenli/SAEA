/****************************************************************************
*项目名称：SAEA.Sockets.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.Core
*类 名 称：RioExtention
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/6 16:36:40
*描述：
*=====================================================================
*修改时间：2019/11/6 16:36:40
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using Pipelines.Sockets.Unofficial;
using SAEA.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// RIO扩展方法
    /// </summary>
    public static class RioExtention
    {

        #region pipelines

        //public static async void SendAsync(this Socket socket, Memory<byte> data, CancellationToken token = default)
        //{
        //    var i = -1;

        //    while (i != 0)
        //    {
        //        i = await socket.SendAsync(data, SocketFlags.None, token);
        //    }
        //}

        /// <summary>
        /// 返回内容,返回以\r\n的数据
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        //public static async Task<Memory<byte>> ReceiveAsync(this Socket socket, CancellationToken token = default)
        //{
        //    List<byte> data = new List<byte>();

        //    var pipe = new Pipe();

        //    _ = WriteAsync(socket, pipe.Writer);

        //    return await ReadAsync(socket, pipe.Reader).WithCancellation(token);
        //}

        //static async Task WriteAsync(Socket socket, PipeWriter writer)
        //{
        //    int bytesRead = 0;

        //    Memory<byte> memory;

        //    while (true)
        //    {
        //        memory = writer.GetMemory(socket.ReceiveBufferSize);

        //        try
        //        {
        //            bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);

        //            if (bytesRead == 0)
        //            {
        //                break;
        //            }
        //            writer.Advance(bytesRead);
        //        }
        //        catch (Exception ex)
        //        {
        //            LogHelper.Error("RioExtention.RioReceiveAsync.WriteAsync", ex);
        //            break;
        //        }

        //        FlushResult flushResult = await writer.FlushAsync();

        //        if (flushResult.IsCompleted)
        //        {
        //            break;
        //        }
        //    }
        //    writer.Complete();
        //}

        //static async Task<Memory<byte>> ReadAsync(Socket socket, PipeReader reader, CancellationToken token = default)
        //{
        //    Memory<byte> result = new Memory<byte>();

        //    List<byte> data = new List<byte>();

        //    while (true)
        //    {
        //        ReadResult readResult = await reader.ReadAsync(token);

        //        ReadOnlySequence<byte> buffer = readResult.Buffer;

        //        SequencePosition? position = null;

        //        do
        //        {
        //            position = buffer.PositionOf((byte)'\n');

        //            if (position != null)
        //            {
        //                data.AddRange(buffer.Slice(0, position.Value).ToArray());

        //                buffer = buffer.Slice(buffer.GetPosition(0, position.Value));
        //            }
        //            else
        //            {
        //                break;
        //            }
        //        }
        //        while (position != null);

        //        reader.AdvanceTo(buffer.Start, buffer.End);

        //        if (readResult.IsCompleted)
        //        {
        //            break;
        //        }
        //    }

        //    reader.Complete();

        //    return result.ToArray().AsMemory();
        //}


        /// <summary>
        /// 发送并接收，
        /// SendAsync、ReceiveAsync
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        //public static async Task<Memory<byte>> RequestAsync(this Socket socket, Memory<byte> data, CancellationToken token = default)
        //{
        //    SendAsync(socket, data, token);

        //    return await ReceiveAsync(socket, token);
        //}

        #endregion

        /// <summary>
        /// 发送并接收，
        /// Pipelines.Sockets.Unofficial
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        //public static async Task<ReadOnlySequence<byte>> Request(this Socket socket, byte[] data, CancellationToken token = default)
        //{
        //    var sc = SocketConnection.Create(socket);

        //    await sc.Output.WriteAsync(data.AsMemory(), token);

        //    var vd = await sc.Input.ReadAsync();

        //    return vd.Buffer;
        //}

    }
}
