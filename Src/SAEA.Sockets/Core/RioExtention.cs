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
*文件名： RioExtention
*版本号： v26.4.23.1
*唯一标识：b7f0acfe-2009-4eaf-a978-c6fb8b25f8a9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/06 20:03:37
*描述：RioExtention接口
*
*=====================================================================
*修改标记
*修改时间：2019/11/06 20:03:37
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RioExtention接口
*
*****************************************************************************/
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
