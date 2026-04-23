/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| _f 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ _f 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Audio.Core
*文件名： AcmChatCodec
*版本号： v26.4.23.1
*唯一标识：4c08aa2c-5ac7-4e87-b119-8a68e4f1eba5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/11 16:46:45
*描述：ACM音频编解码基类
*
*=====================================================================
*修改标记
*修改时间：2021/3/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ACM音频编解码基类
*
*****************************************************************************/

using SAEA.Audio.Base.NAudio;
using SAEA.Audio.Base.NAudio.Wave;
using SAEA.Audio.Base.NAudio.Wave.Compression;
using System;

namespace SAEA.Audio.Core
{
    /// <summary>
    /// useful base class for deriving any chat codecs that will use ACM for decode and encode
    /// </summary>
    abstract class AcmChatCodec : INetworkChatCodec
    {
        private readonly WaveFormat encodeFormat;
        private AcmStream encodeStream;
        private AcmStream decodeStream;
        private int decodeSourceBytesLeftovers;
        private int encodeSourceBytesLeftovers;

        protected AcmChatCodec(WaveFormat recordFormat, WaveFormat encodeFormat)
        {
            RecordFormat = recordFormat;
            this.encodeFormat = encodeFormat;
        }

        public WaveFormat RecordFormat { get; }

        public byte[] Encode(byte[] data, int offset, int length)
        {
            if (encodeStream == null)
            {
                encodeStream = new AcmStream(RecordFormat, encodeFormat);
            }
            //Debug.WriteLine(String.Format("Encoding {0} + {1} bytes", length, encodeSourceBytesLeftovers));
            return Convert(encodeStream, data, offset, length, ref encodeSourceBytesLeftovers);
        }

        public byte[] Decode(byte[] data, int offset, int length)
        {
            if (decodeStream == null)
            {
                decodeStream = new AcmStream(encodeFormat, RecordFormat);
            }
            //Debug.WriteLine(String.Format("Decoding {0} + {1} bytes", data.Length, decodeSourceBytesLeftovers));
            return Convert(decodeStream, data, offset, length, ref decodeSourceBytesLeftovers);
        }

        private static byte[] Convert(AcmStream conversionStream, byte[] data, int offset, int length, ref int sourceBytesLeftovers)
        {
            int bytesInSourceBuffer = length + sourceBytesLeftovers;
            Array.Copy(data, offset, conversionStream.SourceBuffer, sourceBytesLeftovers, length);
            int bytesConverted = conversionStream.Convert(bytesInSourceBuffer, out var sourceBytesConverted);
            sourceBytesLeftovers = bytesInSourceBuffer - sourceBytesConverted;
            if (sourceBytesLeftovers > 0)
            {
                //Debug.WriteLine(String.Format("Asked for {0}, converted {1}", bytesInSourceBuffer, sourceBytesConverted));
                // shift the leftovers down
                Array.Copy(conversionStream.SourceBuffer, sourceBytesConverted, conversionStream.SourceBuffer, 0, sourceBytesLeftovers);
            }
            byte[] encoded = new byte[bytesConverted];
            Array.Copy(conversionStream.DestBuffer, 0, encoded, 0, bytesConverted);
            return encoded;
        }

        public abstract string Name { get; }

        public int BitsPerSecond => encodeFormat.AverageBytesPerSecond * 8;

        public void Dispose()
        {
            if (encodeStream != null)
            {
                encodeStream.Dispose();
                encodeStream = null;
            }
            if (decodeStream != null)
            {
                decodeStream.Dispose();
                decodeStream = null;
            }
        }

        public bool IsAvailable
        {
            get
            {
                // determine if this codec is installed on this PC
                bool available = true;
                try
                {
                    using (new AcmStream(RecordFormat, encodeFormat)) { }
                    using (new AcmStream(encodeFormat, RecordFormat)) { }
                }
                catch (MmException)
                {
                    available = false;
                }
                return available;
            }
        }
    }
}
