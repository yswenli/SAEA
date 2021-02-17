using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class FadeInOutSampleProvider : ISampleProvider
	{
		private enum FadeState
		{
			Silence,
			FadingIn,
			FullVolume,
			FadingOut
		}

		private readonly object lockObject = new object();

		private readonly ISampleProvider source;

		private int fadeSamplePosition;

		private int fadeSampleCount;

		private FadeInOutSampleProvider.FadeState fadeState;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.source.WaveFormat;
			}
		}

		public FadeInOutSampleProvider(ISampleProvider source, bool initiallySilent = false)
		{
			this.source = source;
			this.fadeState = (initiallySilent ? FadeInOutSampleProvider.FadeState.Silence : FadeInOutSampleProvider.FadeState.FullVolume);
		}

		public void BeginFadeIn(double fadeDurationInMilliseconds)
		{
			object obj = this.lockObject;
			lock (obj)
			{
				this.fadeSamplePosition = 0;
				this.fadeSampleCount = (int)(fadeDurationInMilliseconds * (double)this.source.WaveFormat.SampleRate / 1000.0);
				this.fadeState = FadeInOutSampleProvider.FadeState.FadingIn;
			}
		}

		public void BeginFadeOut(double fadeDurationInMilliseconds)
		{
			object obj = this.lockObject;
			lock (obj)
			{
				this.fadeSamplePosition = 0;
				this.fadeSampleCount = (int)(fadeDurationInMilliseconds * (double)this.source.WaveFormat.SampleRate / 1000.0);
				this.fadeState = FadeInOutSampleProvider.FadeState.FadingOut;
			}
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = this.source.Read(buffer, offset, count);
			object obj = this.lockObject;
			lock (obj)
			{
				if (this.fadeState == FadeInOutSampleProvider.FadeState.FadingIn)
				{
					this.FadeIn(buffer, offset, num);
				}
				else if (this.fadeState == FadeInOutSampleProvider.FadeState.FadingOut)
				{
					this.FadeOut(buffer, offset, num);
				}
				else if (this.fadeState == FadeInOutSampleProvider.FadeState.Silence)
				{
					FadeInOutSampleProvider.ClearBuffer(buffer, offset, count);
				}
			}
			return num;
		}

		private static void ClearBuffer(float[] buffer, int offset, int count)
		{
			for (int i = 0; i < count; i++)
			{
				buffer[i + offset] = 0f;
			}
		}

		private void FadeOut(float[] buffer, int offset, int sourceSamplesRead)
		{
			int i = 0;
			while (i < sourceSamplesRead)
			{
				float num = 1f - (float)this.fadeSamplePosition / (float)this.fadeSampleCount;
				for (int j = 0; j < this.source.WaveFormat.Channels; j++)
				{
					buffer[offset + i++] *= num;
				}
				this.fadeSamplePosition++;
				if (this.fadeSamplePosition > this.fadeSampleCount)
				{
					this.fadeState = FadeInOutSampleProvider.FadeState.Silence;
					FadeInOutSampleProvider.ClearBuffer(buffer, i + offset, sourceSamplesRead - i);
					return;
				}
			}
		}

		private void FadeIn(float[] buffer, int offset, int sourceSamplesRead)
		{
			int i = 0;
			while (i < sourceSamplesRead)
			{
				float num = (float)this.fadeSamplePosition / (float)this.fadeSampleCount;
				for (int j = 0; j < this.source.WaveFormat.Channels; j++)
				{
					buffer[offset + i++] *= num;
				}
				this.fadeSamplePosition++;
				if (this.fadeSamplePosition > this.fadeSampleCount)
				{
					this.fadeState = FadeInOutSampleProvider.FadeState.FullVolume;
					return;
				}
			}
		}
	}
}
