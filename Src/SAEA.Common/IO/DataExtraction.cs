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
*命名空间：SAEA.Common.IO
*文件名： DataExtraction
*版本号： v26.4.23.1
*唯一标识：5795f0c0-eb8a-4a90-af7f-ce79229977fb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/26 18:51:09
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/26 18:51:09
*修改人： yswenli
*版本号： v26.4.23.1
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