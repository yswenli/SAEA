using System;

namespace SAEA.Audio.Base.NSpeex
{
	public class SpeexDecoder
	{
		private readonly int sampleRate;

		private float[] decodedData;

		private readonly Bits bits;

		private readonly IDecoder decoder;

		private readonly int frameSize;

		public int FrameSize
		{
			get
			{
				return this.decoder.FrameSize;
			}
		}

		public int SampleRate
		{
			get
			{
				return this.sampleRate;
			}
		}

		public SpeexDecoder(BandMode mode, bool enhanced = true)
		{
			this.bits = new Bits();
			switch (mode)
			{
			case BandMode.Narrow:
				this.decoder = new NbDecoder();
				this.sampleRate = 8000;
				break;
			case BandMode.Wide:
				this.decoder = new SbDecoder(false);
				this.sampleRate = 16000;
				break;
			case BandMode.UltraWide:
				this.decoder = new SbDecoder(true);
				this.sampleRate = 32000;
				break;
			default:
				this.decoder = new NbDecoder();
				this.sampleRate = 8000;
				break;
			}
			this.decoder.PerceptualEnhancement = enhanced;
			this.frameSize = this.decoder.FrameSize;
			this.decodedData = new float[this.sampleRate * 2];
		}

		public int Decode(byte[] inData, int inOffset, int inCount, short[] outData, int outOffset, bool lostFrame)
		{
			if (this.decodedData.Length < outData.Length * 2)
			{
				this.decodedData = new float[outData.Length * 2];
			}
			if (lostFrame || inData == null)
			{
				this.decoder.Decode(null, this.decodedData);
				int i = 0;
				while (i < this.frameSize)
				{
					outData[outOffset] = SpeexDecoder.ConvertToShort(this.decodedData[i]);
					i++;
					outOffset++;
				}
				return this.frameSize;
			}
			this.bits.ReadFrom(inData, inOffset, inCount);
			int num = 0;
			while (this.decoder.Decode(this.bits, this.decodedData) == 0)
			{
				int j = 0;
				while (j < this.frameSize)
				{
					outData[outOffset] = SpeexDecoder.ConvertToShort(this.decodedData[j]);
					j++;
					outOffset++;
				}
				num += this.frameSize;
			}
			return num;
		}

		private static short ConvertToShort(float value)
		{
			if (value > 32767f)
			{
				value = 32767f;
			}
			else if (value < -32768f)
			{
				value = -32768f;
			}
			return (short)Math.Round((double)value);
		}
	}
}
