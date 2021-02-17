using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class Mp3FileReader : WaveStream
    {
        public delegate IMp3FrameDecompressor FrameDecompressorBuilder(WaveFormat mp3Format);

        private readonly WaveFormat waveFormat;

        private Stream mp3Stream;

        private readonly long mp3DataLength;

        private readonly long dataStartPosition;

        private readonly XingHeader xingHeader;

        private readonly bool ownInputStream;

        private List<Mp3Index> tableOfContents;

        private int tocIndex;

        private long totalSamples;

        private readonly int bytesPerSample;

        private readonly int bytesPerDecodedFrame;

        private IMp3FrameDecompressor decompressor;

        private readonly byte[] decompressBuffer;

        private int decompressBufferOffset;

        private int decompressLeftovers;

        private bool repositionedFlag;

        private long position;

        private readonly object repositionLock = new object();

        public Mp3WaveFormat Mp3WaveFormat
        {
            get;
            private set;
        }

        public Id3v2Tag Id3v2Tag
        {
            get; set;
        }

        public byte[] Id3v1Tag
        {
            get; set;
        }

        public override long Length
        {
            get
            {
                return this.totalSamples * (long)this.bytesPerSample;
            }
        }

        public override WaveFormat WaveFormat
        {
            get
            {
                return this.waveFormat;
            }
        }

        public override long Position
        {
            get
            {
                return this.position;
            }
            set
            {
                object obj = this.repositionLock;
                lock (obj)
                {
                    value = Math.Max(Math.Min(value, this.Length), 0L);
                    long num = value / (long)this.bytesPerSample;
                    Mp3Index mp3Index = null;
                    for (int i = 0; i < this.tableOfContents.Count; i++)
                    {
                        if (this.tableOfContents[i].SamplePosition + (long)this.tableOfContents[i].SampleCount > num)
                        {
                            mp3Index = this.tableOfContents[i];
                            this.tocIndex = i;
                            break;
                        }
                    }
                    this.decompressBufferOffset = 0;
                    this.decompressLeftovers = 0;
                    this.repositionedFlag = true;
                    if (mp3Index != null)
                    {
                        this.mp3Stream.Position = mp3Index.FilePosition;
                        long num2 = num - mp3Index.SamplePosition;
                        if (num2 > 0L)
                        {
                            this.decompressBufferOffset = (int)num2 * this.bytesPerSample;
                        }
                    }
                    else
                    {
                        this.mp3Stream.Position = this.mp3DataLength + this.dataStartPosition;
                    }
                    this.position = value;
                }
            }
        }

        public XingHeader XingHeader
        {
            get
            {
                return this.xingHeader;
            }
        }

        public Mp3FileReader(string mp3FileName) : this(File.OpenRead(mp3FileName), new Mp3FileReader.FrameDecompressorBuilder(Mp3FileReader.CreateAcmFrameDecompressor), true)
        {
        }

        public Mp3FileReader(string mp3FileName, Mp3FileReader.FrameDecompressorBuilder frameDecompressorBuilder) : this(File.OpenRead(mp3FileName), frameDecompressorBuilder, true)
        {
        }

        public Mp3FileReader(Stream inputStream) : this(inputStream, new Mp3FileReader.FrameDecompressorBuilder(Mp3FileReader.CreateAcmFrameDecompressor), false)
        {
        }

        public Mp3FileReader(Stream inputStream, Mp3FileReader.FrameDecompressorBuilder frameDecompressorBuilder) : this(inputStream, frameDecompressorBuilder, false)
        {
        }

        private Mp3FileReader(Stream inputStream, Mp3FileReader.FrameDecompressorBuilder frameDecompressorBuilder, bool ownInputStream)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException("inputStream");
            }
            if (frameDecompressorBuilder == null)
            {
                throw new ArgumentNullException("frameDecompressorBuilder");
            }
            this.ownInputStream = ownInputStream;
            try
            {
                this.mp3Stream = inputStream;
                this.Id3v2Tag = Id3v2Tag.ReadTag(this.mp3Stream);
                this.dataStartPosition = this.mp3Stream.Position;
                Mp3Frame mp3Frame = Mp3Frame.LoadFromStream(this.mp3Stream);
                if (mp3Frame == null)
                {
                    throw new InvalidDataException("Invalid MP3 file - no MP3 Frames Detected");
                }
                double num = (double)mp3Frame.BitRate;
                this.xingHeader = XingHeader.LoadXingHeader(mp3Frame);
                if (this.xingHeader != null)
                {
                    this.dataStartPosition = this.mp3Stream.Position;
                }
                Mp3Frame mp3Frame2 = Mp3Frame.LoadFromStream(this.mp3Stream);
                if (mp3Frame2 != null && (mp3Frame2.SampleRate != mp3Frame.SampleRate || mp3Frame2.ChannelMode != mp3Frame.ChannelMode))
                {
                    this.dataStartPosition = mp3Frame2.FileOffset;
                    mp3Frame = mp3Frame2;
                }
                this.mp3DataLength = this.mp3Stream.Length - this.dataStartPosition;
                this.mp3Stream.Position = this.mp3Stream.Length - 128L;
                byte[] array = new byte[128];
                this.mp3Stream.Read(array, 0, 128);
                if (array[0] == 84 && array[1] == 65 && array[2] == 71)
                {
                    this.Id3v1Tag = array;
                    this.mp3DataLength -= 128L;
                }
                this.mp3Stream.Position = this.dataStartPosition;
                this.Mp3WaveFormat = new Mp3WaveFormat(mp3Frame.SampleRate, (mp3Frame.ChannelMode == ChannelMode.Mono) ? 1 : 2, mp3Frame.FrameLength, (int)num);
                this.CreateTableOfContents();
                this.tocIndex = 0;
                num = (double)this.mp3DataLength * 8.0 / this.TotalSeconds();
                this.mp3Stream.Position = this.dataStartPosition;
                this.Mp3WaveFormat = new Mp3WaveFormat(mp3Frame.SampleRate, (mp3Frame.ChannelMode == ChannelMode.Mono) ? 1 : 2, mp3Frame.FrameLength, (int)num);
                this.decompressor = frameDecompressorBuilder(this.Mp3WaveFormat);
                this.waveFormat = this.decompressor.OutputFormat;
                this.bytesPerSample = this.decompressor.OutputFormat.BitsPerSample / 8 * this.decompressor.OutputFormat.Channels;
                this.bytesPerDecodedFrame = 1152 * this.bytesPerSample;
                this.decompressBuffer = new byte[this.bytesPerDecodedFrame * 2];
            }
            catch (Exception)
            {
                if (ownInputStream)
                {
                    inputStream.Dispose();
                }
                throw;
            }
        }

        public static IMp3FrameDecompressor CreateAcmFrameDecompressor(WaveFormat mp3Format)
        {
            return new AcmMp3FrameDecompressor(mp3Format);
        }

        private void CreateTableOfContents()
        {
            try
            {
                this.tableOfContents = new List<Mp3Index>((int)(this.mp3DataLength / 400L));
                Mp3Frame mp3Frame;
                do
                {
                    Mp3Index mp3Index = new Mp3Index();
                    mp3Index.FilePosition = this.mp3Stream.Position;
                    mp3Index.SamplePosition = this.totalSamples;
                    mp3Frame = this.ReadNextFrame(false);
                    if (mp3Frame != null)
                    {
                        this.ValidateFrameFormat(mp3Frame);
                        this.totalSamples += (long)mp3Frame.SampleCount;
                        mp3Index.SampleCount = mp3Frame.SampleCount;
                        mp3Index.ByteCount = (int)(this.mp3Stream.Position - mp3Index.FilePosition);
                        this.tableOfContents.Add(mp3Index);
                    }
                }
                while (mp3Frame != null);
            }
            catch (EndOfStreamException)
            {
            }
        }

        private void ValidateFrameFormat(Mp3Frame frame)
        {
            if (frame.SampleRate != this.Mp3WaveFormat.SampleRate)
            {
                throw new InvalidOperationException(string.Format("Got a frame at sample rate {0}, in an MP3 with sample rate {1}. Mp3FileReader does not support sample rate changes.", frame.SampleRate, this.Mp3WaveFormat.SampleRate));
            }
            if (((frame.ChannelMode == ChannelMode.Mono) ? 1 : 2) != this.Mp3WaveFormat.Channels)
            {
                throw new InvalidOperationException(string.Format("Got a frame with channel mode {0}, in an MP3 with {1} channels. Mp3FileReader does not support changes to channel count.", frame.ChannelMode, this.Mp3WaveFormat.Channels));
            }
        }

        private double TotalSeconds()
        {
            return (double)this.totalSamples / (double)this.Mp3WaveFormat.SampleRate;
        }

        public Mp3Frame ReadNextFrame()
        {
            Mp3Frame mp3Frame = this.ReadNextFrame(true);
            if (mp3Frame != null)
            {
                this.position += (long)(mp3Frame.SampleCount * this.bytesPerSample);
            }
            return mp3Frame;
        }

        private Mp3Frame ReadNextFrame(bool readData)
        {
            Mp3Frame mp3Frame = null;
            try
            {
                mp3Frame = Mp3Frame.LoadFromStream(this.mp3Stream, readData);
                if (mp3Frame != null)
                {
                    this.tocIndex++;
                }
            }
            catch (EndOfStreamException)
            {
            }
            return mp3Frame;
        }

        public override int Read(byte[] sampleBuffer, int offset, int numBytes)
        {
            int i = 0;
            object obj = this.repositionLock;
            lock (obj)
            {
                if (this.decompressLeftovers != 0)
                {
                    int num = Math.Min(this.decompressLeftovers, numBytes);
                    Array.Copy(this.decompressBuffer, this.decompressBufferOffset, sampleBuffer, offset, num);
                    this.decompressLeftovers -= num;
                    if (this.decompressLeftovers == 0)
                    {
                        this.decompressBufferOffset = 0;
                    }
                    else
                    {
                        this.decompressBufferOffset += num;
                    }
                    i += num;
                    offset += num;
                }
                int num2 = this.tocIndex;
                if (this.repositionedFlag)
                {
                    this.decompressor.Reset();
                    this.tocIndex = Math.Max(0, this.tocIndex - 3);
                    this.mp3Stream.Position = this.tableOfContents[this.tocIndex].FilePosition;
                    this.repositionedFlag = false;
                }
                while (i < numBytes)
                {
                    Mp3Frame mp3Frame = this.ReadNextFrame(true);
                    if (mp3Frame == null)
                    {
                        break;
                    }
                    int num3 = this.decompressor.DecompressFrame(mp3Frame, this.decompressBuffer, 0);
                    if (this.tocIndex > num2 && num3 != 0)
                    {
                        if (this.tocIndex == num2 + 1 && num3 == this.bytesPerDecodedFrame * 2)
                        {
                            Array.Copy(this.decompressBuffer, this.bytesPerDecodedFrame, this.decompressBuffer, 0, this.bytesPerDecodedFrame);
                            num3 = this.bytesPerDecodedFrame;
                        }
                        int num4 = Math.Min(num3 - this.decompressBufferOffset, numBytes - i);
                        Array.Copy(this.decompressBuffer, this.decompressBufferOffset, sampleBuffer, offset, num4);
                        if (num4 + this.decompressBufferOffset < num3)
                        {
                            this.decompressBufferOffset = num4 + this.decompressBufferOffset;
                            this.decompressLeftovers = num3 - this.decompressBufferOffset;
                        }
                        else
                        {
                            this.decompressBufferOffset = 0;
                            this.decompressLeftovers = 0;
                        }
                        offset += num4;
                        i += num4;
                    }
                }
            }
            this.position += (long)i;
            return i;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.mp3Stream != null)
                {
                    if (this.ownInputStream)
                    {
                        this.mp3Stream.Dispose();
                    }
                    this.mp3Stream = null;
                }
                if (this.decompressor != null)
                {
                    this.decompressor.Dispose();
                    this.decompressor = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
