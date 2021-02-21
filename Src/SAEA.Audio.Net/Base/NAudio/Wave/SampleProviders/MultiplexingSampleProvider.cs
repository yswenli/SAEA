using SAEA.Audio.Base.NAudio.Utils;
using System;
using System.Collections.Generic;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class MultiplexingSampleProvider : ISampleProvider
	{
		private readonly IList<ISampleProvider> inputs;

		private readonly WaveFormat waveFormat;

		private readonly int outputChannelCount;

		private readonly int inputChannelCount;

		private readonly List<int> mappings;

		private float[] inputBuffer;

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

		public MultiplexingSampleProvider(IEnumerable<ISampleProvider> inputs, int numberOfOutputChannels)
		{
			this.inputs = new List<ISampleProvider>(inputs);
			this.outputChannelCount = numberOfOutputChannels;
			if (this.inputs.Count == 0)
			{
				throw new ArgumentException("You must provide at least one input");
			}
			if (numberOfOutputChannels < 1)
			{
				throw new ArgumentException("You must provide at least one output");
			}
			foreach (ISampleProvider current in this.inputs)
			{
				if (this.waveFormat == null)
				{
					if (current.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
					{
						throw new ArgumentException("Only 32 bit float is supported");
					}
					this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(current.WaveFormat.SampleRate, numberOfOutputChannels);
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
			this.mappings = new List<int>();
			for (int i = 0; i < this.outputChannelCount; i++)
			{
				this.mappings.Add(i % this.inputChannelCount);
			}
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = count / this.outputChannelCount;
			int num2 = 0;
			int num3 = 0;
			foreach (ISampleProvider current in this.inputs)
			{
				int num4 = num * current.WaveFormat.Channels;
				this.inputBuffer = BufferHelpers.Ensure(this.inputBuffer, num4);
				int num5 = current.Read(this.inputBuffer, 0, num4);
				num3 = Math.Max(num3, num5 / current.WaveFormat.Channels);
				for (int i = 0; i < current.WaveFormat.Channels; i++)
				{
					int num6 = num2 + i;
					for (int j = 0; j < this.outputChannelCount; j++)
					{
						if (this.mappings[j] == num6)
						{
							int num7 = i;
							int num8 = offset + j;
							int k;
							for (k = 0; k < num; k++)
							{
								if (num7 >= num5)
								{
									break;
								}
								buffer[num8] = this.inputBuffer[num7];
								num8 += this.outputChannelCount;
								num7 += current.WaveFormat.Channels;
							}
							while (k < num)
							{
								buffer[num8] = 0f;
								num8 += this.outputChannelCount;
								k++;
							}
						}
					}
				}
				num2 += current.WaveFormat.Channels;
			}
			return num3 * this.outputChannelCount;
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
