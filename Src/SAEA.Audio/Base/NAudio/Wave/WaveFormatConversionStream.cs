using SAEA.Audio.Base.NAudio.Wave.Compression;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class WaveFormatConversionStream : WaveStream
    {
        private readonly WaveFormatConversionProvider conversionProvider;

        private readonly WaveFormat targetFormat;

        private readonly long length;

        private long position;

        private readonly WaveStream sourceStream;

        private bool isDisposed;

        public override long Position
        {
            get
            {
                return this.position;
            }
            set
            {
                value -= value % (long)this.BlockAlign;
                long num = this.EstimateDestToSource(value);
                this.sourceStream.Position = num;
                this.position = this.EstimateSourceToDest(this.sourceStream.Position);
                this.conversionProvider.Reposition();
            }
        }

        public override long Length
        {
            get
            {
                return this.length;
            }
        }

        public override WaveFormat WaveFormat
        {
            get
            {
                return this.targetFormat;
            }
        }

        public WaveFormatConversionStream(WaveFormat targetFormat, WaveStream sourceStream)
        {
            this.sourceStream = sourceStream;
            this.targetFormat = targetFormat;
            this.conversionProvider = new WaveFormatConversionProvider(targetFormat, sourceStream);
            this.length = this.EstimateSourceToDest((long)((int)sourceStream.Length));
            this.position = 0L;
        }

        public static WaveStream CreatePcmStream(WaveStream sourceStream)
        {
            if (sourceStream.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
            {
                return sourceStream;
            }
            WaveFormat waveFormat = AcmStream.SuggestPcmFormat(sourceStream.WaveFormat);
            if (waveFormat.SampleRate < 8000)
            {
                if (sourceStream.WaveFormat.Encoding != WaveFormatEncoding.G723)
                {
                    throw new InvalidOperationException("Invalid suggested output format, please explicitly provide a target format");
                }
                waveFormat = new WaveFormat(8000, 16, 1);
            }
            return new WaveFormatConversionStream(waveFormat, sourceStream);
        }

        [Obsolete("can be unreliable, use of this method not encouraged")]
        public int SourceToDest(int source)
        {
            return (int)this.EstimateSourceToDest((long)source);
        }

        private long EstimateSourceToDest(long source)
        {
            long expr_20 = source * (long)this.targetFormat.AverageBytesPerSecond / (long)this.sourceStream.WaveFormat.AverageBytesPerSecond;
            return expr_20 - expr_20 % (long)this.targetFormat.BlockAlign;
        }

        private long EstimateDestToSource(long dest)
        {
            long expr_20 = dest * (long)this.sourceStream.WaveFormat.AverageBytesPerSecond / (long)this.targetFormat.AverageBytesPerSecond;
            return (long)((int)(expr_20 - expr_20 % (long)this.sourceStream.WaveFormat.BlockAlign));
        }

        [Obsolete("can be unreliable, use of this method not encouraged")]
        public int DestToSource(int dest)
        {
            return (int)this.EstimateDestToSource((long)dest);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num = this.conversionProvider.Read(buffer, offset, count);
            this.position += (long)num;
            return num;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                if (disposing)
                {
                    this.sourceStream.Dispose();
                    this.conversionProvider.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
