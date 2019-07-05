/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：Http2Response
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 14:33:50
*描述：
*=====================================================================
*修改时间：2019/6/28 14:33:50
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Http2.Net
{
    public class Http2Response : Http2Result, IDisposable
    {
        IStream _stream;

        List<byte> _data;

        internal Http2Response(IStream stream)
        {
            _stream = stream;
            this.Heads = new List<HeaderField>();
            _data = new List<byte>();
        }

        public void SetHeaders(HeaderField[] headerFields)
        {
            foreach (var item in headerFields)
            {
                SetHeader(item);
            }
        }

        public void SetHeader(HeaderField headerField)
        {
            if (string.IsNullOrEmpty(headerField.Name)) return;

            var hf = Heads.Where(b => b.Name == headerField.Name).FirstOrDefault();

            if (!string.IsNullOrEmpty(hf.Name))
            {
                Heads.Remove(hf);
            }
            Heads.Add(headerField);
        }

        public void SetHeader(string key, string value)
        {
            var headerField = new HeaderField()
            {
                Name = key,
                Value = value,
                Sensitive = false
            };
            SetHeader(headerField);
        }

        public void Write(byte[] data)
        {
            _data.AddRange(data);
        }

        public void Write(string data)
        {
            _data.AddRange(Encoding.UTF8.GetBytes(data));
        }

        public async void FlushAsync()
        {
            await _stream.WriteHeadersAsync(Heads, false);
            Heads.Clear();
            if (_data.Any())
                await _stream.WriteAsync(new ArraySegment<byte>(_data.ToArray()), false);
            _data.Clear();
        }

        public async void End()
        {
            var fh = new FrameHeader
            {
                Type = FrameType.GoAway,
                StreamId = 0,
                Flags = 0,
            };

            var goAwayData = new GoAwayFrameData
            {
                Reason = new GoAwayReason
                {
                    LastStreamId = _stream.Id,
                    ErrorCode = ErrorCode.StreamClosed,
                    DebugData = Constants.EmptyByteArray,
                }
            };

            await writer.WriteGoAway(fh, goAwayData, true);
        }


        public void Dispose()
        {
            if (Heads != null && Heads.Any()) Heads.Clear();
            if (_data != null && _data.Any()) _data.Clear();
        }
    }
}
