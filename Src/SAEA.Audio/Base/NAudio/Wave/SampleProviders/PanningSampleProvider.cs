using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class PanningSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider source;

		private float pan;

		private float leftMultiplier;

		private float rightMultiplier;

		private readonly WaveFormat waveFormat;

		private float[] sourceBuffer;

		private IPanStrategy panStrategy;

		public float Pan
		{
			get
			{
				return this.pan;
			}
			set
			{
				if (value < -1f || value > 1f)
				{
					throw new ArgumentOutOfRangeException("value", "Pan must be in the range -1 to 1");
				}
				this.pan = value;
				this.UpdateMultipliers();
			}
		}

		public IPanStrategy PanStrategy
		{
			get
			{
				return this.panStrategy;
			}
			set
			{
				this.panStrategy = value;
				this.UpdateMultipliers();
			}
		}

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public PanningSampleProvider(ISampleProvider source)
		{
			if (source.WaveFormat.Channels != 1)
			{
				throw new ArgumentException("Source sample provider must be mono");
			}
			this.source = source;
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
			this.panStrategy = new SinPanStrategy();
		}

		private void UpdateMultipliers()
		{
			StereoSamplePair multipliers = this.panStrategy.GetMultipliers(this.Pan);
			this.leftMultiplier = multipliers.Left;
			this.rightMultiplier = multipliers.Right;
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = count / 2;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			int num2 = this.source.Read(this.sourceBuffer, 0, num);
			int num3 = offset;
			for (int i = 0; i < num2; i++)
			{
				buffer[num3++] = this.leftMultiplier * this.sourceBuffer[i];
				buffer[num3++] = this.rightMultiplier * this.sourceBuffer[i];
			}
			return num2 * 2;
		}
	}
}
