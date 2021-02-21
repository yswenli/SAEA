using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class Vbr
	{
		private const int VBR_MEMORY_SIZE = 5;

		private const int MIN_ENERGY = 6000;

		private const float NOISE_POW = 0.3f;

		public static readonly float[][] nb_thresh = new float[][]
		{
			new float[]
			{
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f
			},
			new float[]
			{
				3.5f,
				2.5f,
				2f,
				1.2f,
				0.5f,
				0f,
				-0.5f,
				-0.7f,
				-0.8f,
				-0.9f,
				-1f
			},
			new float[]
			{
				10f,
				6.5f,
				5.2f,
				4.5f,
				3.9f,
				3.5f,
				3f,
				2.5f,
				2.3f,
				1.8f,
				1f
			},
			new float[]
			{
				11f,
				8.8f,
				7.5f,
				6.5f,
				5f,
				3.9f,
				3.9f,
				3.9f,
				3.5f,
				3f,
				1f
			},
			new float[]
			{
				11f,
				11f,
				9.9f,
				9f,
				8f,
				7f,
				6.5f,
				6f,
				5f,
				4f,
				2f
			},
			new float[]
			{
				11f,
				11f,
				11f,
				11f,
				9.5f,
				9f,
				8f,
				7f,
				6.5f,
				5f,
				3f
			},
			new float[]
			{
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				9.5f,
				8.5f,
				8f,
				6.5f,
				4f
			},
			new float[]
			{
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				9.8f,
				7.5f,
				5.5f
			},
			new float[]
			{
				8f,
				5f,
				3.7f,
				3f,
				2.5f,
				2f,
				1.8f,
				1.5f,
				1f,
				0f,
				0f
			}
		};

		public static readonly float[][] hb_thresh = new float[][]
		{
			new float[]
			{
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f
			},
			new float[]
			{
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f
			},
			new float[]
			{
				11f,
				11f,
				9.5f,
				8.5f,
				7.5f,
				6f,
				5f,
				3.9f,
				3f,
				2f,
				1f
			},
			new float[]
			{
				11f,
				11f,
				11f,
				11f,
				11f,
				9.5f,
				8.7f,
				7.8f,
				7f,
				6.5f,
				4f
			},
			new float[]
			{
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				11f,
				9.8f,
				7.5f,
				5.5f
			}
		};

		public static readonly float[][] uhb_thresh = new float[][]
		{
			new float[]
			{
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f,
				-1f
			},
			new float[]
			{
				3.9f,
				2.5f,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				-1f
			}
		};

		private float energy_alpha;

		private float average_energy;

		private float last_energy;

		private float[] last_log_energy;

		private float accum_sum;

		private float last_pitch_coef;

		private float soft_pitch;

		private float last_quality;

		private float noise_level;

		private float noise_accum;

		private float noise_accum_count;

		private int consec_noise;

		public Vbr()
		{
			this.average_energy = 0f;
			this.last_energy = 1f;
			this.accum_sum = 0f;
			this.energy_alpha = 0.1f;
			this.soft_pitch = 0f;
			this.last_pitch_coef = 0f;
			this.last_quality = 0f;
			this.noise_accum = (float)(0.05 * Math.Pow(6000.0, 0.30000001192092896));
			this.noise_accum_count = 0.05f;
			this.noise_level = this.noise_accum / this.noise_accum_count;
			this.consec_noise = 0;
			this.last_log_energy = new float[5];
			for (int i = 0; i < 5; i++)
			{
				this.last_log_energy[i] = (float)Math.Log(6000.0);
			}
		}

		public float Analysis(float[] sig, int len, int pitch, float pitch_coef)
		{
			float num = 0f;
			float num2 = 0f;
			float num3 = 7f;
			float num4 = 0f;
			for (int i = 0; i < len >> 1; i++)
			{
				num += sig[i] * sig[i];
			}
			for (int i = len >> 1; i < len; i++)
			{
				num2 += sig[i] * sig[i];
			}
			float num5 = num + num2;
			float num6 = (float)Math.Log((double)(num5 + 6000f));
			for (int i = 0; i < 5; i++)
			{
				num4 += (num6 - this.last_log_energy[i]) * (num6 - this.last_log_energy[i]);
			}
			num4 /= 150f;
			if (num4 > 1f)
			{
				num4 = 1f;
			}
			float num7 = 3f * (pitch_coef - 0.4f) * Math.Abs(pitch_coef - 0.4f);
			this.average_energy = (1f - this.energy_alpha) * this.average_energy + this.energy_alpha * num5;
			this.noise_level = this.noise_accum / this.noise_accum_count;
			float num8 = (float)Math.Pow((double)num5, 0.30000001192092896);
			if (this.noise_accum_count < 0.06f && num5 > 6000f)
			{
				this.noise_accum = 0.05f * num8;
			}
			if ((num7 < 0.3f && num4 < 0.2f && num8 < 1.2f * this.noise_level) || (num7 < 0.3f && num4 < 0.05f && num8 < 1.5f * this.noise_level) || (num7 < 0.4f && num4 < 0.05f && num8 < 1.2f * this.noise_level) || (num7 < 0f && num4 < 0.05f))
			{
				this.consec_noise++;
				float num9;
				if (num8 > 3f * this.noise_level)
				{
					num9 = 3f * this.noise_level;
				}
				else
				{
					num9 = num8;
				}
				if (this.consec_noise >= 4)
				{
					this.noise_accum = 0.95f * this.noise_accum + 0.05f * num9;
					this.noise_accum_count = 0.95f * this.noise_accum_count + 0.05f;
				}
			}
			else
			{
				this.consec_noise = 0;
			}
			if (num8 < this.noise_level && num5 > 6000f)
			{
				this.noise_accum = 0.95f * this.noise_accum + 0.05f * num8;
				this.noise_accum_count = 0.95f * this.noise_accum_count + 0.05f;
			}
			if (num5 < 30000f)
			{
				num3 -= 0.7f;
				if (num5 < 10000f)
				{
					num3 -= 0.7f;
				}
				if (num5 < 3000f)
				{
					num3 -= 0.7f;
				}
			}
			else
			{
				float num10 = (float)Math.Log((double)((num5 + 1f) / (1f + this.last_energy)));
				float num11 = (float)Math.Log((double)((num5 + 1f) / (1f + this.average_energy)));
				if (num11 < -5f)
				{
					num11 = -5f;
				}
				if (num11 > 2f)
				{
					num11 = 2f;
				}
				if (num11 > 0f)
				{
					num3 += 0.6f * num11;
				}
				if (num11 < 0f)
				{
					num3 += 0.5f * num11;
				}
				if (num10 > 0f)
				{
					if (num10 > 5f)
					{
						num10 = 5f;
					}
					num3 += 0.5f * num10;
				}
				if (num2 > 1.6f * num)
				{
					num3 += 0.5f;
				}
			}
			this.last_energy = num5;
			this.soft_pitch = 0.6f * this.soft_pitch + 0.4f * pitch_coef;
			num3 += (float)(2.2000000476837158 * ((double)pitch_coef - 0.4 + ((double)this.soft_pitch - 0.4)));
			if (num3 < this.last_quality)
			{
				num3 = 0.5f * num3 + 0.5f * this.last_quality;
			}
			if (num3 < 4f)
			{
				num3 = 4f;
			}
			if (num3 > 10f)
			{
				num3 = 10f;
			}
			if (this.consec_noise >= 3)
			{
				num3 = 4f;
			}
			if (this.consec_noise != 0)
			{
				num3 -= (float)(1.0 * (Math.Log(3.0 + (double)this.consec_noise) - Math.Log(3.0)));
			}
			if (num3 < 0f)
			{
				num3 = 0f;
			}
			if (num5 < 60000f)
			{
				if (this.consec_noise > 2)
				{
					num3 -= (float)(0.5 * (Math.Log(3.0 + (double)this.consec_noise) - Math.Log(3.0)));
				}
				if (num5 < 10000f && this.consec_noise > 2)
				{
					num3 -= (float)(0.5 * (Math.Log(3.0 + (double)this.consec_noise) - Math.Log(3.0)));
				}
				if (num3 < 0f)
				{
					num3 = 0f;
				}
				num3 += (float)(0.3 * Math.Log((double)num5 / 60000.0));
			}
			if (num3 < -1f)
			{
				num3 = -1f;
			}
			this.last_pitch_coef = pitch_coef;
			this.last_quality = num3;
			for (int i = 4; i > 0; i--)
			{
				this.last_log_energy[i] = this.last_log_energy[i - 1];
			}
			this.last_log_energy[0] = num6;
			return num3;
		}
	}
}
