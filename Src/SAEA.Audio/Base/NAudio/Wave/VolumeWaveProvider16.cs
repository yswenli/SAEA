using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class VolumeWaveProvider16 : IWaveProvider
	{
		private readonly IWaveProvider sourceProvider;

		private float volume;

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

		public WaveFormat WaveFormat
		{
			get
			{
				return this.sourceProvider.WaveFormat;
			}
		}

		public VolumeWaveProvider16(IWaveProvider sourceProvider)
		{
			this.Volume = 1f;
			this.sourceProvider = sourceProvider;
			if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
			{
				throw new ArgumentException("Expecting PCM input");
			}
			if (sourceProvider.WaveFormat.BitsPerSample != 16)
			{
				throw new ArgumentException("Expecting 16 bit");
			}
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			int num = this.sourceProvider.Read(buffer, offset, count);
			if (this.volume == 0f)
			{
				for (int i = 0; i < num; i++)
				{
					buffer[offset++] = 0;
				}
			}
			else if (this.volume != 1f)
			{
				for (int j = 0; j < num; j += 2)
				{
					short num2 = (short)((int)buffer[offset + 1] << 8 | (int)buffer[offset]);
					float num3 = (float)num2 * this.volume;
					num2 = (short)num3;
					if (this.Volume > 1f)
					{
						if (num3 > 32767f)
						{
							num2 = 32767;
						}
						else if (num3 < -32768f)
						{
							num2 = -32768;
						}
					}
					buffer[offset++] = (byte)(num2 & 255);
					buffer[offset++] = (byte)(num2 >> 8);
				}
			}
			return num;
		}
	}
}
