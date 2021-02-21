using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public class AdpcmWaveFormat : WaveFormat
	{
		private short samplesPerBlock;

		private short numCoeff;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
		private short[] coefficients;

		public int SamplesPerBlock
		{
			get
			{
				return (int)this.samplesPerBlock;
			}
		}

		public int NumCoefficients
		{
			get
			{
				return (int)this.numCoeff;
			}
		}

		public short[] Coefficients
		{
			get
			{
				return this.coefficients;
			}
		}

		private AdpcmWaveFormat() : this(8000, 1)
		{
		}

		public AdpcmWaveFormat(int sampleRate, int channels) : base(sampleRate, 0, channels)
		{
			this.waveFormatTag = WaveFormatEncoding.Adpcm;
			this.extraSize = 32;
			int sampleRate2 = this.sampleRate;
			if (sampleRate2 <= 11025)
			{
				if (sampleRate2 == 8000 || sampleRate2 == 11025)
				{
					this.blockAlign = 256;
					goto IL_70;
				}
			}
			else
			{
				if (sampleRate2 == 22050)
				{
					this.blockAlign = 512;
					goto IL_70;
				}
				if (sampleRate2 != 44100)
				{
				}
			}
			this.blockAlign = 1024;
			IL_70:
			this.bitsPerSample = 4;
			this.samplesPerBlock = (short)(((int)this.blockAlign - 7 * channels) * 8 / ((int)this.bitsPerSample * channels) + 2);
			this.averageBytesPerSecond = base.SampleRate * (int)this.blockAlign / (int)this.samplesPerBlock;
			this.numCoeff = 7;
			this.coefficients = new short[]
			{
				256,
				0,
				512,
				-256,
				0,
				0,
				192,
				64,
				240,
				0,
				460,
				-208,
				392,
				-232
			};
		}

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(this.samplesPerBlock);
			writer.Write(this.numCoeff);
			short[] array = this.coefficients;
			for (int i = 0; i < array.Length; i++)
			{
				short value = array[i];
				writer.Write(value);
			}
		}

		public override string ToString()
		{
			return string.Format("Microsoft ADPCM {0} Hz {1} channels {2} bits per sample {3} samples per block", new object[]
			{
				base.SampleRate,
				this.channels,
				this.bitsPerSample,
				this.samplesPerBlock
			});
		}
	}
}
