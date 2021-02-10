using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class MonoToStereoSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider source;

		private float[] sourceBuffer;

		public WaveFormat WaveFormat
		{
			get;
			private set;
		}

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

		public MonoToStereoSampleProvider(ISampleProvider source)
		{
			this.LeftVolume = 1f;
			this.RightVolume = 1f;
			if (source.WaveFormat.Channels != 1)
			{
				throw new ArgumentException("Source must be mono");
			}
			this.source = source;
			this.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int count2 = count / 2;
			int num = offset;
			this.EnsureSourceBuffer(count2);
			int num2 = this.source.Read(this.sourceBuffer, 0, count2);
			for (int i = 0; i < num2; i++)
			{
				buffer[num++] = this.sourceBuffer[i] * this.LeftVolume;
				buffer[num++] = this.sourceBuffer[i] * this.RightVolume;
			}
			return num2 * 2;
		}

		private void EnsureSourceBuffer(int count)
		{
			if (this.sourceBuffer == null || this.sourceBuffer.Length < count)
			{
				this.sourceBuffer = new float[count];
			}
		}
	}
}
