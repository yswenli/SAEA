using System;
using System.Collections.Generic;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class MixingWaveProvider32 : IWaveProvider
	{
		private List<IWaveProvider> inputs;

		private WaveFormat waveFormat;

		private int bytesPerSample;

		public int InputCount
		{
			get
			{
				return this.inputs.Count;
			}
		}

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public MixingWaveProvider32()
		{
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
			this.bytesPerSample = 4;
			this.inputs = new List<IWaveProvider>();
		}

		public MixingWaveProvider32(IEnumerable<IWaveProvider> inputs) : this()
		{
			foreach (IWaveProvider current in inputs)
			{
				this.AddInputStream(current);
			}
		}

		public void AddInputStream(IWaveProvider waveProvider)
		{
			if (waveProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Must be IEEE floating point", "waveProvider.WaveFormat");
			}
			if (waveProvider.WaveFormat.BitsPerSample != 32)
			{
				throw new ArgumentException("Only 32 bit audio currently supported", "waveProvider.WaveFormat");
			}
			if (this.inputs.Count == 0)
			{
				int sampleRate = waveProvider.WaveFormat.SampleRate;
				int channels = waveProvider.WaveFormat.Channels;
				this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
			}
			else if (!waveProvider.WaveFormat.Equals(this.waveFormat))
			{
				throw new ArgumentException("All incoming channels must have the same format", "waveProvider.WaveFormat");
			}
			List<IWaveProvider> obj = this.inputs;
			lock (obj)
			{
				this.inputs.Add(waveProvider);
			}
		}

		public void RemoveInputStream(IWaveProvider waveProvider)
		{
			List<IWaveProvider> obj = this.inputs;
			lock (obj)
			{
				this.inputs.Remove(waveProvider);
			}
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			if (count % this.bytesPerSample != 0)
			{
				throw new ArgumentException("Must read an whole number of samples", "count");
			}
			Array.Clear(buffer, offset, count);
			int num = 0;
			byte[] array = new byte[count];
			List<IWaveProvider> obj = this.inputs;
			lock (obj)
			{
				using (List<IWaveProvider>.Enumerator enumerator = this.inputs.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						int num2 = enumerator.Current.Read(array, 0, count);
						num = Math.Max(num, num2);
						if (num2 > 0)
						{
							MixingWaveProvider32.Sum32BitAudio(buffer, offset, array, num2);
						}
					}
				}
			}
			return num;
		}

		private unsafe static void Sum32BitAudio(byte[] destBuffer, int offset, byte[] sourceBuffer, int bytesRead)
		{
			fixed (byte* ptr = &destBuffer[offset], ptr2 = &sourceBuffer[0])
			{
				float* ptr3 = (float*)ptr;
				float* ptr4 = (float*)ptr2;
				int num = bytesRead / 4;
				for (int i = 0; i < num; i++)
				{
					ptr3[i] += ptr4[i];
				}
			}
		}
	}
}
