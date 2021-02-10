using System;

namespace SAEA.Audio.NSpeex
{
	internal interface IDecoder
	{
		bool PerceptualEnhancement
		{
			get;
			set;
		}

		int FrameSize
		{
			get;
		}

		bool Dtx
		{
			get;
		}

		float[] PiGain
		{
			get;
		}

		float[] Exc
		{
			get;
		}

		float[] Innov
		{
			get;
		}

		int Decode(Bits bits, float[] xout);

		void DecodeStereo(float[] data, int frameSize);
	}
}
