using SAEA.Audio.Base.NAudio.Dsp;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
    public class WdlResamplingSampleProvider : ISampleProvider
    {
        private readonly WdlResampler resampler;

        private readonly WaveFormat outFormat;

        private readonly ISampleProvider source;

        private readonly int channels;

        public WaveFormat WaveFormat
        {
            get
            {
                return this.outFormat;
            }
        }

        public WdlResamplingSampleProvider(ISampleProvider source, int newSampleRate)
        {
            this.channels = source.WaveFormat.Channels;
            this.outFormat = WaveFormat.CreateIeeeFloatWaveFormat(newSampleRate, this.channels);
            this.source = source;
            this.resampler = new WdlResampler();
            this.resampler.SetMode(true, 2, false, 64, 32);
            this.resampler.SetFilterParms(0.693f, 0.707f);
            this.resampler.SetFeedMode(false);
            this.resampler.SetRates((double)source.WaveFormat.SampleRate, (double)newSampleRate);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int num = count / this.channels;
            float[] buffer2;
            int offset2;
            int num2 = this.resampler.ResamplePrepare(num, this.outFormat.Channels, out buffer2, out offset2);
            int nsamples_in = this.source.Read(buffer2, offset2, num2 * this.channels) / this.channels;
            return this.resampler.ResampleOut(buffer, offset, nsamples_in, num, this.channels) * this.channels;
        }
    }
}
