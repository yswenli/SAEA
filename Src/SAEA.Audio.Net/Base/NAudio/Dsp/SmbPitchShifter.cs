using System;

namespace SAEA.Audio.Base.NAudio.Dsp
{
	public class SmbPitchShifter
	{
		private static int MAX_FRAME_LENGTH = 16000;

		private float[] gInFIFO = new float[SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gOutFIFO = new float[SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gFFTworksp = new float[2 * SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gLastPhase = new float[SmbPitchShifter.MAX_FRAME_LENGTH / 2 + 1];

		private float[] gSumPhase = new float[SmbPitchShifter.MAX_FRAME_LENGTH / 2 + 1];

		private float[] gOutputAccum = new float[2 * SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gAnaFreq = new float[SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gAnaMagn = new float[SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gSynFreq = new float[SmbPitchShifter.MAX_FRAME_LENGTH];

		private float[] gSynMagn = new float[SmbPitchShifter.MAX_FRAME_LENGTH];

		private long gRover;

		public void PitchShift(float pitchShift, long numSampsToProcess, float sampleRate, float[] indata)
		{
			this.PitchShift(pitchShift, numSampsToProcess, 2048L, 10L, sampleRate, indata);
		}

		public void PitchShift(float pitchShift, long numSampsToProcess, long fftFrameSize, long osamp, float sampleRate, float[] indata)
		{
			long num = fftFrameSize / 2L;
			long num2 = fftFrameSize / osamp;
			double num3 = (double)sampleRate / (double)fftFrameSize;
			double num4 = 6.2831853071795862 * (double)num2 / (double)fftFrameSize;
			long num5 = fftFrameSize - num2;
			if (this.gRover == 0L)
			{
				this.gRover = num5;
			}
			for (long num6 = 0L; num6 < numSampsToProcess; num6 += 1L)
			{
				checked
				{
					this.gInFIFO[(int)((IntPtr)this.gRover)] = indata[(int)((IntPtr)num6)];
					indata[(int)((IntPtr)num6)] = this.gOutFIFO[(int)((IntPtr)(unchecked(this.gRover - num5)))];
				}
				this.gRover += 1L;
				if (this.gRover >= fftFrameSize)
				{
					this.gRover = num5;
					for (long num7 = 0L; num7 < fftFrameSize; num7 += 1L)
					{
						double num8 = -0.5 * Math.Cos(6.2831853071795862 * (double)num7 / (double)fftFrameSize) + 0.5;
						this.gFFTworksp[(int)(checked((IntPtr)(unchecked(2L * num7))))] = (float)((double)this.gInFIFO[(int)(checked((IntPtr)num7))] * num8);
						this.gFFTworksp[(int)(checked((IntPtr)(unchecked(2L * num7 + 1L))))] = 0f;
					}
					this.ShortTimeFourierTransform(this.gFFTworksp, fftFrameSize, -1L);
					for (long num7 = 0L; num7 <= num; num7 += 1L)
					{
						double num9;
						double num10;
						checked
						{
							num9 = (double)this.gFFTworksp[(int)((IntPtr)(unchecked(2L * num7)))];
							num10 = (double)this.gFFTworksp[(int)((IntPtr)(unchecked(2L * num7 + 1L)))];
						}
						double num11 = 2.0 * Math.Sqrt(num9 * num9 + num10 * num10);
						double num12 = Math.Atan2(num10, num9);
						double num13 = num12 - (double)this.gLastPhase[(int)(checked((IntPtr)num7))];
						this.gLastPhase[(int)(checked((IntPtr)num7))] = (float)num12;
						num13 -= (double)num7 * num4;
						long num14 = (long)(num13 / 3.1415926535897931);
						if (num14 >= 0L)
						{
							num14 += (num14 & 1L);
						}
						else
						{
							num14 -= (num14 & 1L);
						}
						num13 -= 3.1415926535897931 * (double)num14;
						num13 = (double)osamp * num13 / 6.2831853071795862;
						num13 = (double)num7 * num3 + num13 * num3;
						checked
						{
							this.gAnaMagn[(int)((IntPtr)num7)] = (float)num11;
							this.gAnaFreq[(int)((IntPtr)num7)] = (float)num13;
						}
					}
					int num15 = 0;
					while ((long)num15 < fftFrameSize)
					{
						this.gSynMagn[num15] = 0f;
						this.gSynFreq[num15] = 0f;
						num15++;
					}
					for (long num7 = 0L; num7 <= num; num7 += 1L)
					{
						long num16 = (long)((float)num7 * pitchShift);
						if (num16 <= num)
						{
							this.gSynMagn[(int)(checked((IntPtr)num16))] += this.gAnaMagn[(int)(checked((IntPtr)num7))];
							this.gSynFreq[(int)(checked((IntPtr)num16))] = this.gAnaFreq[(int)(checked((IntPtr)num7))] * pitchShift;
						}
					}
					for (long num7 = 0L; num7 <= num; num7 += 1L)
					{
						double num11;
						double num13;
						checked
						{
							num11 = (double)this.gSynMagn[(int)((IntPtr)num7)];
							num13 = (double)this.gSynFreq[(int)((IntPtr)num7)];
						}
						num13 -= (double)num7 * num3;
						num13 /= num3;
						num13 = 6.2831853071795862 * num13 / (double)osamp;
						num13 += (double)num7 * num4;
						this.gSumPhase[(int)(checked((IntPtr)num7))] += (float)num13;
						double num12 = (double)this.gSumPhase[(int)(checked((IntPtr)num7))];
						this.gFFTworksp[(int)(checked((IntPtr)(unchecked(2L * num7))))] = (float)(num11 * Math.Cos(num12));
						this.gFFTworksp[(int)(checked((IntPtr)(unchecked(2L * num7 + 1L))))] = (float)(num11 * Math.Sin(num12));
					}
					for (long num7 = fftFrameSize + 2L; num7 < 2L * fftFrameSize; num7 += 1L)
					{
						this.gFFTworksp[(int)(checked((IntPtr)num7))] = 0f;
					}
					this.ShortTimeFourierTransform(this.gFFTworksp, fftFrameSize, 1L);
					for (long num7 = 0L; num7 < fftFrameSize; num7 += 1L)
					{
						double num8 = -0.5 * Math.Cos(6.2831853071795862 * (double)num7 / (double)fftFrameSize) + 0.5;
						this.gOutputAccum[(int)(checked((IntPtr)num7))] += (float)(2.0 * num8 * (double)this.gFFTworksp[(int)(checked((IntPtr)(unchecked(2L * num7))))] / (double)(num * osamp));
					}
					for (long num7 = 0L; num7 < num2; num7 += 1L)
					{
						checked
						{
							this.gOutFIFO[(int)((IntPtr)num7)] = this.gOutputAccum[(int)((IntPtr)num7)];
						}
					}
					for (long num7 = 0L; num7 < fftFrameSize; num7 += 1L)
					{
						checked
						{
							this.gOutputAccum[(int)((IntPtr)num7)] = this.gOutputAccum[(int)((IntPtr)(unchecked(num7 + num2)))];
						}
					}
					for (long num7 = 0L; num7 < num5; num7 += 1L)
					{
						checked
						{
							this.gInFIFO[(int)((IntPtr)num7)] = this.gInFIFO[(int)((IntPtr)(unchecked(num7 + num2)))];
						}
					}
				}
			}
		}

		public void ShortTimeFourierTransform(float[] fftBuffer, long fftFrameSize, long sign)
		{
			for (long num = 2L; num < 2L * fftFrameSize - 2L; num += 2L)
			{
				long num2 = 2L;
				long num3 = 0L;
				while (num2 < 2L * fftFrameSize)
				{
					if ((num & num2) != 0L)
					{
						num3 += 1L;
					}
					num3 <<= 1;
					num2 <<= 1;
				}
				checked
				{
					if (num < num3)
					{
						float num4 = fftBuffer[(int)((IntPtr)num)];
						fftBuffer[(int)((IntPtr)num)] = fftBuffer[(int)((IntPtr)num3)];
						fftBuffer[(int)((IntPtr)num3)] = num4;
						num4 = fftBuffer[(int)((IntPtr)(unchecked(num + 1L)))];
						fftBuffer[(int)((IntPtr)(unchecked(num + 1L)))] = fftBuffer[(int)((IntPtr)(unchecked(num3 + 1L)))];
						fftBuffer[(int)((IntPtr)(unchecked(num3 + 1L)))] = num4;
					}
				}
			}
			long num5 = (long)(Math.Log((double)fftFrameSize) / Math.Log(2.0) + 0.5);
			long num6 = 0L;
			long num7 = 2L;
			while (num6 < num5)
			{
				num7 <<= 1;
				long num8 = num7 >> 1;
				float num9 = 1f;
				float num10 = 0f;
				float num11 = 3.14159274f / (float)(num8 >> 1);
				float num12 = (float)Math.Cos((double)num11);
				float num13 = (float)((double)sign * Math.Sin((double)num11));
				for (long num3 = 0L; num3 < num8; num3 += 2L)
				{
					float num14;
					for (long num = num3; num < 2L * fftFrameSize; num += num7)
					{
						num14 = fftBuffer[(int)(checked((IntPtr)(unchecked(num + num8))))] * num9 - fftBuffer[(int)(checked((IntPtr)(unchecked(num + num8 + 1L))))] * num10;
						float num15 = fftBuffer[(int)(checked((IntPtr)(unchecked(num + num8))))] * num10 + fftBuffer[(int)(checked((IntPtr)(unchecked(num + num8 + 1L))))] * num9;
						fftBuffer[(int)(checked((IntPtr)(unchecked(num + num8))))] = fftBuffer[(int)(checked((IntPtr)num))] - num14;
						fftBuffer[(int)(checked((IntPtr)(unchecked(num + num8 + 1L))))] = fftBuffer[(int)(checked((IntPtr)(unchecked(num + 1L))))] - num15;
						fftBuffer[(int)(checked((IntPtr)num))] += num14;
						fftBuffer[(int)(checked((IntPtr)(unchecked(num + 1L))))] += num15;
					}
					num14 = num9 * num12 - num10 * num13;
					num10 = num9 * num13 + num10 * num12;
					num9 = num14;
				}
				num6 += 1L;
			}
		}
	}
}
