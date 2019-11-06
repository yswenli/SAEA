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
using SAEA.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// RIO扩展方法
    /// </summary>
    public static class RioExtention
    {
        /// <summary>
        /// 发送请求并返回内容,返回以\r\n的数据
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static async Task<Byte[][]> Request(this Socket socket)
        {
            var pipe = new Pipe();

            var writer = pipe.Writer;

            var reader = pipe.Reader;

            #region write

            await Task.Run(async () =>
            {
                int bytesRead = 0;

                Memory<byte> memory;

                while (true)
                {
                    memory = writer.GetMemory(socket.ReceiveBufferSize);

                    try
                    {
                        //bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);

                        if (bytesRead == 0)
                        {
                            break;
                        }
                        writer.Advance(bytesRead);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("RioExtention.Request", ex);
                        break;
                    }

                    FlushResult flushResult = await writer.FlushAsync();

                    if (flushResult.IsCompleted)
                    {
                        break;
                    }
                }
                writer.Complete();
            });


            #endregion

            #region read

            List<byte[]> result = new List<byte[]>();

            while (true)
            {
                ReadResult readResult = await reader.ReadAsync();

                ReadOnlySequence<byte> buffer = readResult.Buffer;

                SequencePosition? position = null;

                do
                {
                    position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        result.Add(buffer.Slice(0, position.Value).ToArray());

                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                }
                while (position != null);

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted)
                {
                    break;
                }
            }

            reader.Complete();
            #endregion

            return result.ToArray();
        }
    }
}
