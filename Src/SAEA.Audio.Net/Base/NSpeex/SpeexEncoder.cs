using System;

namespace SAEA.Audio.Base.NSpeex
{
	public class SpeexEncoder
	{
		public const string Version = ".Net Speex Encoder v0.0.1";

		private readonly IEncoder encoder;

		private readonly Bits bits;

		private readonly float[] rawData;

		private readonly int frameSize;

		public int SampleRate
		{
			get
			{
				return this.encoder.SamplingRate;
			}
		}

		public int Quality
		{
			set
			{
				this.encoder.Quality = value;
			}
		}

		public bool VBR
		{
			get
			{
				return this.encoder.Vbr;
			}
			set
			{
				this.encoder.Vbr = value;
			}
		}

		public int FrameSize
		{
			get
			{
				return this.frameSize;
			}
		}

		public SpeexEncoder(BandMode mode)
		{
			this.bits = new Bits();
			switch (mode)
			{
			case BandMode.Narrow:
				this.encoder = new NbEncoder();
				break;
			case BandMode.Wide:
				this.encoder = new SbEncoder(false);
				break;
			case BandMode.UltraWide:
				this.encoder = new SbEncoder(true);
				break;
			default:
				throw new ArgumentException("Invalid mode", "mode");
			}
			this.frameSize = this.encoder.FrameSize;
			this.rawData = new float[this.frameSize];
		}

		public int Encode(short[] inData, int inOffset, int inCount, byte[] outData, int outOffset, int outCount)
		{
			this.bits.Reset();
			int i = 0;
			int num = 0;
			while (i < inCount)
			{
				for (int j = 0; j < this.frameSize; j++)
				{
					this.rawData[j] = (float)inData[inOffset + j + i];
				}
				num += this.encoder.Encode(this.bits, this.rawData);
				i += this.frameSize;
			}
			if (num == 0)
			{
				return 0;
			}
			return this.bits.Write(outData, outOffset, outCount);
		}
	}
}
