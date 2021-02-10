using System;

namespace SAEA.Audio.NSpeex
{
	internal class Misc
	{
		public static float[] Window(int windowSize, int subFrameSize)
		{
			int num = subFrameSize * 7 / 2;
			int num2 = subFrameSize * 5 / 2;
			float[] array = new float[windowSize];
			for (int i = 0; i < num; i++)
			{
				array[i] = (float)(0.54 - 0.46 * Math.Cos(3.1415926535897931 * (double)i / (double)num));
			}
			for (int i = 0; i < num2; i++)
			{
				array[num + i] = (float)(0.54 + 0.46 * Math.Cos(3.1415926535897931 * (double)i / (double)num2));
			}
			return array;
		}

		public static float[] LagWindow(int lpcSize, float lagFactor)
		{
			float[] array = new float[lpcSize + 1];
			for (int i = 0; i < lpcSize + 1; i++)
			{
				array[i] = (float)Math.Exp(-0.5 * (6.2831853071795862 * (double)lagFactor * (double)i) * (6.2831853071795862 * (double)lagFactor * (double)i));
			}
			return array;
		}
	}
}
