using SAEA.Audio.NAudio.Dsp;
using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
    public class AdsrSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        private readonly EnvelopeGenerator adsr;

        private float attackSeconds;

        private float releaseSeconds;

        public float AttackSeconds
        {
            get
            {
                return this.attackSeconds;
            }
            set
            {
                this.attackSeconds = value;
                this.adsr.AttackRate = this.attackSeconds * (float)this.WaveFormat.SampleRate;
            }
        }

        public float ReleaseSeconds
        {
            get
            {
                return this.releaseSeconds;
            }
            set
            {
                this.releaseSeconds = value;
                this.adsr.ReleaseRate = this.releaseSeconds * (float)this.WaveFormat.SampleRate;
            }
        }

        public WaveFormat WaveFormat
        {
            get
            {
                return this.source.WaveFormat;
            }
        }

        public AdsrSampleProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels > 1)
            {
                throw new ArgumentException("Currently only supports mono inputs");
            }
            this.source = source;
            this.adsr = new EnvelopeGenerator();
            this.AttackSeconds = 0.01f;
            this.adsr.SustainLevel = 1f;
            this.adsr.DecayRate = 0f * (float)this.WaveFormat.SampleRate;
            this.ReleaseSeconds = 0.3f;
            this.adsr.Gate(true);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (this.adsr.State == EnvelopeGenerator.EnvelopeState.Idle)
            {
                return 0;
            }
            int num = this.source.Read(buffer, offset, count);
            for (int i = 0; i < num; i++)
            {
                buffer[offset++] *= this.adsr.Process();
            }
            return num;
        }

        public void Stop()
        {
            this.adsr.Gate(false);
        }
    }
}
