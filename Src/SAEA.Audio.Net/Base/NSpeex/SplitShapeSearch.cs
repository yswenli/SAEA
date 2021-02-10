using System;

namespace SAEA.Audio.NSpeex
{
	internal class SplitShapeSearch : CodebookSearch
	{
		private const int MAX_COMPLEXITY = 10;

		private int subframesize;

		private int subvect_size;

		private int nb_subvect;

		private int[] shape_cb;

		private int shape_cb_size;

		private int shape_bits;

		private int have_sign;

		private int[] ind;

		private int[] signs;

		private float[] E;

		private float[] t;

		private float[] r2;

		private float[] e;

		private float[][] ot;

		private float[][] nt;

		private int[,] nind;

		private int[,] oind;

		public SplitShapeSearch(int subframesize_0, int subvect_size_1, int nb_subvect_2, int[] shape_cb_3, int shape_bits_4, int have_sign_5)
		{
			this.subframesize = subframesize_0;
			this.subvect_size = subvect_size_1;
			this.nb_subvect = nb_subvect_2;
			this.shape_cb = shape_cb_3;
			this.shape_bits = shape_bits_4;
			this.have_sign = have_sign_5;
			this.ind = new int[nb_subvect_2];
			this.signs = new int[nb_subvect_2];
			this.shape_cb_size = 1 << shape_bits_4;
			this.ot = this.CreateJaggedArray<float>(10, subframesize_0);
			this.nt = this.CreateJaggedArray<float>(10, subframesize_0);
			this.oind = new int[10, nb_subvect_2];
			this.nind = new int[10, nb_subvect_2];
			this.t = new float[subframesize_0];
			this.e = new float[subframesize_0];
			this.r2 = new float[subframesize_0];
			this.E = new float[this.shape_cb_size];
		}

		private T[][] CreateJaggedArray<T>(int dim1, int dim2)
		{
			T[][] array = new T[dim1][];
			for (int i = 0; i < dim1; i++)
			{
				array[i] = new T[dim2];
				Array.Clear(array[i], 0, dim2);
			}
			return array;
		}

		public sealed override void Quantify(float[] target, float[] ak, float[] awk1, float[] awk2, int p, int nsf, float[] exc, int es, float[] r, Bits bits, int complexity)
		{
			int num = complexity;
			if (num > 10)
			{
				num = 10;
			}
			float[] array = new float[this.shape_cb_size * this.subvect_size];
			int[] array2 = new int[num];
			float[] array3 = new float[num];
			float[] array4 = new float[num];
			float[] array5 = new float[num];
			int[] array6 = new int[num];
			int[] array7 = new int[num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < this.nb_subvect; j++)
				{
					this.nind[i, j] = (this.oind[i, j] = -1);
				}
			}
			for (int j = 0; j < num; j++)
			{
				for (int i = 0; i < nsf; i++)
				{
					this.ot[j][i] = target[i];
				}
			}
			for (int i = 0; i < this.shape_cb_size; i++)
			{
				int num2 = i * this.subvect_size;
				int num3 = i * this.subvect_size;
				for (int j = 0; j < this.subvect_size; j++)
				{
					array[num2 + j] = 0f;
					for (int k = 0; k <= j; k++)
					{
						array[num2 + j] += 0.03125f * (float)this.shape_cb[num3 + k] * r[j - k];
					}
				}
				this.E[i] = 0f;
				for (int j = 0; j < this.subvect_size; j++)
				{
					this.E[i] += array[num2 + j] * array[num2 + j];
				}
			}
			for (int j = 0; j < num; j++)
			{
				array5[j] = 0f;
			}
			for (int i = 0; i < this.nb_subvect; i++)
			{
				int num4 = i * this.subvect_size;
				for (int j = 0; j < num; j++)
				{
					array4[j] = 2.14748365E+09f;
				}
				for (int j = 0; j < num; j++)
				{
					array6[j] = (array7[j] = 0);
				}
				for (int j = 0; j < num; j++)
				{
					float num5 = 0f;
					for (int l = num4; l < num4 + this.subvect_size; l++)
					{
						num5 += this.ot[j][l] * this.ot[j][l];
					}
					num5 *= 0.5f;
					if (this.have_sign != 0)
					{
						VQ.Nbest_sign(this.ot[j], num4, array, this.subvect_size, this.shape_cb_size, this.E, num, array2, array3);
					}
					else
					{
						VQ.Nbest(this.ot[j], num4, array, this.subvect_size, this.shape_cb_size, this.E, num, array2, array3);
					}
					for (int k = 0; k < num; k++)
					{
						float num6 = array5[j] + array3[k] + num5;
						if (num6 < array4[num - 1])
						{
							for (int l = 0; l < num; l++)
							{
								if (num6 < array4[l])
								{
									int m;
									for (m = num - 1; m > l; m--)
									{
										array4[m] = array4[m - 1];
										array6[m] = array6[m - 1];
										array7[m] = array7[m - 1];
									}
									array4[l] = num6;
									array6[m] = array2[k];
									array7[m] = j;
									break;
								}
							}
						}
					}
					if (i == 0)
					{
						break;
					}
				}
				for (int j = 0; j < num; j++)
				{
					for (int l = (i + 1) * this.subvect_size; l < nsf; l++)
					{
						this.nt[j][l] = this.ot[array7[j]][l];
					}
					for (int l = 0; l < this.subvect_size; l++)
					{
						float num7 = 1f;
						int num8 = array6[j];
						if (num8 >= this.shape_cb_size)
						{
							num7 = -1f;
							num8 -= this.shape_cb_size;
						}
						int n = this.subvect_size - l;
						float num9 = num7 * 0.03125f * (float)this.shape_cb[num8 * this.subvect_size + l];
						int m = 0;
						int num10 = num4 + this.subvect_size;
						while (m < nsf - this.subvect_size * (i + 1))
						{
							this.nt[j][num10] -= num9 * r[m + n];
							m++;
							num10++;
						}
					}
					for (int n = 0; n < this.nb_subvect; n++)
					{
						this.nind[j, n] = this.oind[array7[j], n];
					}
					this.nind[j, i] = array6[j];
				}
				float[][] array8 = this.ot;
				this.ot = this.nt;
				this.nt = array8;
				for (int j = 0; j < num; j++)
				{
					for (int l = 0; l < this.nb_subvect; l++)
					{
						this.oind[j, l] = this.nind[j, l];
					}
				}
				for (int j = 0; j < num; j++)
				{
					array5[j] = array4[j];
				}
			}
			for (int i = 0; i < this.nb_subvect; i++)
			{
				this.ind[i] = this.nind[0, i];
				bits.Pack(this.ind[i], this.shape_bits + this.have_sign);
			}
			for (int i = 0; i < this.nb_subvect; i++)
			{
				float num11 = 1f;
				int num12 = this.ind[i];
				if (num12 >= this.shape_cb_size)
				{
					num11 = -1f;
					num12 -= this.shape_cb_size;
				}
				for (int j = 0; j < this.subvect_size; j++)
				{
					this.e[this.subvect_size * i + j] = num11 * 0.03125f * (float)this.shape_cb[num12 * this.subvect_size + j];
				}
			}
			for (int j = 0; j < nsf; j++)
			{
				exc[es + j] += this.e[j];
			}
			Filters.Syn_percep_zero(this.e, 0, ak, awk1, awk2, this.r2, nsf, p);
			for (int j = 0; j < nsf; j++)
			{
				target[j] -= this.r2[j];
			}
		}

		public sealed override void Unquantify(float[] exc, int es, int nsf, Bits bits)
		{
			for (int i = 0; i < this.nb_subvect; i++)
			{
				if (this.have_sign != 0)
				{
					this.signs[i] = bits.Unpack(1);
				}
				else
				{
					this.signs[i] = 0;
				}
				this.ind[i] = bits.Unpack(this.shape_bits);
			}
			for (int i = 0; i < this.nb_subvect; i++)
			{
				float num = 1f;
				if (this.signs[i] != 0)
				{
					num = -1f;
				}
				for (int j = 0; j < this.subvect_size; j++)
				{
					exc[es + this.subvect_size * i + j] += num * 0.03125f * (float)this.shape_cb[this.ind[i] * this.subvect_size + j];
				}
			}
		}
	}
}
