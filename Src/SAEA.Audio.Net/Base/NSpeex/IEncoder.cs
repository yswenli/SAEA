using System;

namespace SAEA.Audio.NSpeex
{
	internal interface IEncoder
	{
		int EncodedFrameSize
		{
			get;
		}

		int FrameSize
		{
			get;
		}

		int Quality
		{
			set;
		}

		int BitRate
		{
			get;
			set;
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

		int Mode
		{
			get;
			set;
		}

		bool Vbr
		{
			get;
			set;
		}

		bool Vad
		{
			get;
			set;
		}

		bool Dtx
		{
			get;
			set;
		}

		int Abr
		{
			get;
			set;
		}

		float VbrQuality
		{
			get;
			set;
		}

		int Complexity
		{
			get;
			set;
		}

		int SamplingRate
		{
			get;
			set;
		}

		int LookAhead
		{
			get;
		}

		float RelativeQuality
		{
			get;
		}

		int Encode(Bits bits, float[] ins0);
	}
}
