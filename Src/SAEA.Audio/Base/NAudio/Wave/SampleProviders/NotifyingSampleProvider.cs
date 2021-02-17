using System;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class NotifyingSampleProvider : ISampleProvider, ISampleNotifier
	{
		private readonly ISampleProvider source;

		private readonly SampleEventArgs sampleArgs = new SampleEventArgs(0f, 0f);

		private readonly int channels;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler<SampleEventArgs> Sample;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.source.WaveFormat;
			}
		}

		public NotifyingSampleProvider(ISampleProvider source)
		{
			this.source = source;
			this.channels = this.WaveFormat.Channels;
		}

		public int Read(float[] buffer, int offset, int sampleCount)
		{
			int num = this.source.Read(buffer, offset, sampleCount);
			if (this.Sample != null)
			{
				for (int i = 0; i < num; i += this.channels)
				{
					this.sampleArgs.Left = buffer[offset + i];
					this.sampleArgs.Right = ((this.channels > 1) ? buffer[offset + i + 1] : this.sampleArgs.Left);
					this.Sample(this, this.sampleArgs);
				}
			}
			return num;
		}
	}
}
