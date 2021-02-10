using SAEA.Audio.NAudio.Utils;
using System;
using System.Collections.Generic;

namespace SAEA.Audio.NAudio.Wave
{
	public class MultiplexingWaveProvider : IWaveProvider
	{
		private readonly IList<IWaveProvider> inputs;

		private readonly WaveFormat waveFormat;

		private readonly int outputChannelCount;

		private readonly int inputChannelCount;

		private readonly List<int> mappings;

		private readonly int bytesPerSample;

		private byte[] inputBuffer;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public int InputChannelCount
		{
			get
			{
				return this.inputChannelCount;
			}
		}

		public int OutputChannelCount
		{
			get
			{
				return this.outputChannelCount;
			}
		}

		public MultiplexingWaveProvider(IEnumerable<IWaveProvider> inputs, int numberOfOutputChannels)
		{
			this.inputs = new List<IWaveProvider>(inputs);
			this.outputChannelCount = numberOfOutputChannels;
			if (this.inputs.Count == 0)
			{
				throw new ArgumentException("You must provide at least one input");
			}
			if (numberOfOutputChannels < 1)
			{
				throw new ArgumentException("You must provide at least one output");
			}
			foreach (IWaveProvider current in this.inputs)
			{
				if (this.waveFormat == null)
				{
					if (current.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
					{
						this.waveFormat = new WaveFormat(current.WaveFormat.SampleRate, current.WaveFormat.BitsPerSample, numberOfOutputChannels);
					}
					else
					{
						if (current.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
						{
							throw new ArgumentException("Only PCM and 32 bit float are supported");
						}
						this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(current.WaveFormat.SampleRate, numberOfOutputChannels);
					}
				}
				else
				{
					if (current.WaveFormat.BitsPerSample != this.waveFormat.BitsPerSample)
					{
						throw new ArgumentException("All inputs must have the same bit depth");
					}
					if (current.WaveFormat.SampleRate != this.waveFormat.SampleRate)
					{
						throw new ArgumentException("All inputs must have the same sample rate");
					}
				}
				this.inputChannelCount += current.WaveFormat.Channels;
			}
			this.bytesPerSample = this.waveFormat.BitsPerSample / 8;
			this.mappings = new List<int>();
			for (int i = 0; i < this.outputChannelCount; i++)
			{
				this.mappings.Add(i % this.inputChannelCount);
			}
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			int num = this.bytesPerSample * this.outputChannelCount;
			int num2 = count / num;
			int num3 = 0;
			int num4 = 0;
			foreach (IWaveProvider current in this.inputs)
			{
				int num5 = this.bytesPerSample * current.WaveFormat.Channels;
				int num6 = num2 * num5;
				this.inputBuffer = BufferHelpers.Ensure(this.inputBuffer, num6);
				int num7 = current.Read(this.inputBuffer, 0, num6);
				num4 = Math.Max(num4, num7 / num5);
				for (int i = 0; i < current.WaveFormat.Channels; i++)
				{
					int num8 = num3 + i;
					for (int j = 0; j < this.outputChannelCount; j++)
					{
						if (this.mappings[j] == num8)
						{
							int num9 = i * this.bytesPerSample;
							int num10 = offset + j * this.bytesPerSample;
							int k;
							for (k = 0; k < num2; k++)
							{
								if (num9 >= num7)
								{
									break;
								}
								Array.Copy(this.inputBuffer, num9, buffer, num10, this.bytesPerSample);
								num10 += num;
								num9 += num5;
							}
							while (k < num2)
							{
								Array.Clear(buffer, num10, this.bytesPerSample);
								num10 += num;
								k++;
							}
						}
					}
				}
				num3 += current.WaveFormat.Channels;
			}
			return num4 * num;
		}

		public void ConnectInputToOutput(int inputChannel, int outputChannel)
		{
			if (inputChannel < 0 || inputChannel >= this.InputChannelCount)
			{
				throw new ArgumentException("Invalid input channel");
			}
			if (outputChannel < 0 || outputChannel >= this.OutputChannelCount)
			{
				throw new ArgumentException("Invalid output channel");
			}
			this.mappings[outputChannel] = inputChannel;
		}
	}
}
