using System;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class StereoToMonoSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider sourceProvider;

		private float[] sourceBuffer;

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
            get;private set;
		}

		public StereoToMonoSampleProvider(ISampleProvider sourceProvider)
		{
			this.LeftVolume = 0.5f;
			this.RightVolume = 0.5f;
			if (sourceProvider.WaveFormat.Channels != 2)
			{
				throw new ArgumentException("Source must be stereo");
			}
			this.sourceProvider = sourceProvider;
			this.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceProvider.WaveFormat.SampleRate, 1);
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = count * 2;
			if (this.sourceBuffer == null || this.sourceBuffer.Length < num)
			{
				this.sourceBuffer = new float[num];
			}
			int num2 = this.sourceProvider.Read(this.sourceBuffer, 0, num);
			int num3 = offset / 2;
			for (int i = 0; i < num2; i += 2)
			{
				float arg_59_0 = this.sourceBuffer[i];
				float num4 = this.sourceBuffer[i + 1];
				float num5 = arg_59_0 * this.LeftVolume + num4 * this.RightVolume;
				buffer[num3++] = num5;
			}
			return num2 / 2;
		}
	}
}
