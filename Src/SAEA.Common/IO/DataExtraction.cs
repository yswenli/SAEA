/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom.IO
*文件名： DataExtraction
*版本号： v7.0.0.1
*唯一标识：bf3043aa-a84d-42ab-a6b6-b3adf2ab8925
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:53:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:53:26
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.IO
{
    public class DataExtraction
    {
        Pipe _pipe;

        PipeWriter _writer;

        PipeReader _reader;

        static readonly byte[] _enter = new byte[] { 13, 10 };

        public DataExtraction()
        {
            _pipe = new Pipe();

            _writer = _pipe.Writer;

            _reader = _pipe.Reader;
        }


        public async ValueTask WriteAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _writer.WriteAsync(memory, cancellationToken);
        }


        public async ValueTask<string> ReadLineAsync()
        {
            ReadResult result = await _reader.ReadAsync();

            ReadOnlySequence<byte> buffer = result.Buffer;

            if(TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
            {
                return Encoding.UTF8.GetString(line.ToArray());
            }

            return null;
        }

        bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }
            line = buffer.Slice(0, position.Value);
            _reader.AdvanceTo(position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }



        public async ValueTask<string> ReadBlock(int len, CancellationToken ctoken)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                while (!ctoken.IsCancellationRequested && (sb.Length < len && Encoding.UTF8.GetByteCount(sb.ToString()) < len))
                {
                    sb.Append(await ReadLineAsync());
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "-Err:ReadBlock Timeout," + ex.Message;
            }
        }


        public async ValueTask CompleteAsync()
        {
            await _reader.CompleteAsync();
        }
    }
}
