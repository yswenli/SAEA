using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Wave
{
    public class MonoToStereoProvider16 : IWaveProvider
    {
        private IWaveProvider sourceProvider;

        private WaveFormat outputFormat;

        private byte[] sourceBuffer;

        public float LeftVolume
        {
            get;
            set;
        }

        public float RightVolume
        {
            get;
            set;
        }

        public WaveFormat WaveFormat
        {
            get
            {
                return this.outputFormat;
            }
        }

        public MonoToStereoProvider16(IWaveProvider sourceProvider)
        {
            if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                throw new ArgumentException("Source must be PCM");
            }
            if (sourceProvider.WaveFormat.Channels != 1)
            {
                throw new ArgumentException("Source must be Mono");
            }
            if (sourceProvider.WaveFormat.BitsPerSample != 16)
            {
                throw new ArgumentException("Source must be 16 bit");
            }
            this.sourceProvider = sourceProvider;
            this.outputFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 2);
            this.RightVolume = 1f;
            this.LeftVolume = 1f;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int num = count / 2;
            this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
            WaveBuffer waveBuffer = new WaveBuffer(this.sourceBuffer);
            WaveBuffer waveBuffer2 = new WaveBuffer(buffer);
            int num2 = this.sourceProvider.Read(this.sourceBuffer, 0, num) / 2;
            int num3 = offset / 2;
            for (int i = 0; i < num2; i++)
            {
                short num4 = waveBuffer.ShortBuffer[i];
                waveBuffer2.ShortBuffer[num3++] = (short)(this.LeftVolume * (float)num4);
                waveBuffer2.ShortBuffer[num3++] = (short)(this.RightVolume * (float)num4);
            }
            return num2 * 4;
        }
    }
}
