using SAEA.Audio.NAudio.Wave.SampleProviders;
using System;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.NAudio.Wave
{
    public class WaveChannel32 : WaveStream, ISampleNotifier
    {
        private WaveStream sourceStream;

        private readonly WaveFormat waveFormat;

        private readonly long length;

        private readonly int destBytesPerSample;

        private readonly int sourceBytesPerSample;

        private volatile float volume;

        private volatile float pan;

        private long position;

        private readonly ISampleChunkConverter sampleProvider;

        private readonly object lockObject = new object();

        private SampleEventArgs sampleEventArgs = new SampleEventArgs(0f, 0f);

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event EventHandler<SampleEventArgs> Sample;

        public override int BlockAlign
        {
            get
            {
                return (int)this.SourceToDest((long)this.sourceStream.BlockAlign);
            }
        }

        public override long Length
        {
            get
            {
                return this.length;
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
                object obj = this.lockObject;
                lock (obj)
                {
                    value -= value % (long)this.BlockAlign;
                    if (value < 0L)
                    {
                        this.sourceStream.Position = 0L;
                    }
                    else
                    {
                        this.sourceStream.Position = this.DestToSource(value);
                    }
                    this.position = this.SourceToDest(this.sourceStream.Position);
                }
            }
        }

        public bool PadWithZeroes
        {
            get;
            set;
        }

        public override WaveFormat WaveFormat
        {
            get
            {
                return this.waveFormat;
            }
        }

        public float Volume
        {
            get
            {
                return this.volume;
            }
            set
            {
                this.volume = value;
            }
        }

        public float Pan
        {
            get
            {
                return this.pan;
            }
            set
            {
                this.pan = value;
            }
        }

        public WaveChannel32(WaveStream sourceStream, float volume, float pan)
        {
            this.PadWithZeroes = true;
            ISampleChunkConverter[] array = new ISampleChunkConverter[]
            {
                new Mono8SampleChunkConverter(),
                new Stereo8SampleChunkConverter(),
                new Mono16SampleChunkConverter(),
                new Stereo16SampleChunkConverter(),
                new Mono24SampleChunkConverter(),
                new Stereo24SampleChunkConverter(),
                new MonoFloatSampleChunkConverter(),
                new StereoFloatSampleChunkConverter()
            };
            for (int i = 0; i < array.Length; i++)
            {
                ISampleChunkConverter sampleChunkConverter = array[i];
                if (sampleChunkConverter.Supports(sourceStream.WaveFormat))
                {
                    this.sampleProvider = sampleChunkConverter;
                    break;
                }
            }
            if (this.sampleProvider == null)
            {
                throw new ArgumentException("Unsupported sourceStream format");
            }
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceStream.WaveFormat.SampleRate, 2);
            this.destBytesPerSample = 8;
            this.sourceStream = sourceStream;
            this.volume = volume;
            this.pan = pan;
            this.sourceBytesPerSample = sourceStream.WaveFormat.Channels * sourceStream.WaveFormat.BitsPerSample / 8;
            this.length = this.SourceToDest(sourceStream.Length);
            this.position = 0L;
        }

        private long SourceToDest(long sourceBytes)
        {
            return sourceBytes / (long)this.sourceBytesPerSample * (long)this.destBytesPerSample;
        }

        private long DestToSource(long destBytes)
        {
            return destBytes / (long)this.destBytesPerSample * (long)this.sourceBytesPerSample;
        }

        public WaveChannel32(WaveStream sourceStream) : this(sourceStream, 1f, 0f)
        {
        }

        public override int Read(byte[] destBuffer, int offset, int numBytes)
        {
            object obj = this.lockObject;
            int result;
            lock (obj)
            {
                int num = 0;
                WaveBuffer waveBuffer = new WaveBuffer(destBuffer);
                if (this.position < 0L)
                {
                    num = (int)Math.Min((long)numBytes, 0L - this.position);
                    for (int i = 0; i < num; i++)
                    {
                        destBuffer[i + offset] = 0;
                    }
                }
                if (num < numBytes)
                {
                    this.sampleProvider.LoadNextChunk(this.sourceStream, (numBytes - num) / 8);
                    int num2 = offset / 4 + num / 4;
                    float num3;
                    float num4;
                    while (this.sampleProvider.GetNextSample(out num3, out num4) && num < numBytes)
                    {
                        num3 = ((this.pan <= 0f) ? num3 : (num3 * (1f - this.pan) / 2f));
                        num4 = ((this.pan >= 0f) ? num4 : (num4 * (this.pan + 1f) / 2f));
                        num3 *= this.volume;
                        num4 *= this.volume;
                        waveBuffer.FloatBuffer[num2++] = num3;
                        waveBuffer.FloatBuffer[num2++] = num4;
                        num += 8;
                        if (this.Sample != null)
                        {
                            this.RaiseSample(num3, num4);
                        }
                    }
                }
                if (this.PadWithZeroes && num < numBytes)
                {
                    Array.Clear(destBuffer, offset + num, numBytes - num);
                    num = numBytes;
                }
                this.position += (long)num;
                result = num;
            }
            return result;
        }

        public override bool HasData(int count)
        {
            return this.sourceStream.HasData(count) && this.position + (long)count >= 0L && this.position < this.length && this.volume != 0f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.sourceStream != null)
            {
                this.sourceStream.Dispose();
                this.sourceStream = null;
            }
            base.Dispose(disposing);
        }

        private void RaiseSample(float left, float right)
        {
            this.sampleEventArgs.Left = left;
            this.sampleEventArgs.Right = right;
            this.Sample(this, this.sampleEventArgs);
        }
    }
}
