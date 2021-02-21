using SAEA.Audio.Base.NAudio.Dmo;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class ResamplerDmoStream : WaveStream
	{
		private readonly IWaveProvider inputProvider;

		private readonly WaveStream inputStream;

		private readonly WaveFormat outputFormat;

		private DmoOutputDataBuffer outputBuffer;

		private DmoResampler dmoResampler;

		private MediaBuffer inputMediaBuffer;

		private long position;

		public override WaveFormat WaveFormat
		{
			get
			{
				return this.outputFormat;
			}
		}

		public override long Length
		{
			get
			{
				if (this.inputStream == null)
				{
					throw new InvalidOperationException("Cannot report length if the input was an IWaveProvider");
				}
				return this.InputToOutputPosition(this.inputStream.Length);
			}
		}

		public override long Position
		{
			get
			{
				return this.position;
			}
			set
			{
				if (this.inputStream == null)
				{
					throw new InvalidOperationException("Cannot set position if the input was not a WaveStream");
				}
				this.inputStream.Position = this.OutputToInputPosition(value);
				this.position = this.InputToOutputPosition(this.inputStream.Position);
				this.dmoResampler.MediaObject.Discontinuity(0);
			}
		}

		public ResamplerDmoStream(IWaveProvider inputProvider, WaveFormat outputFormat)
		{
			this.inputProvider = inputProvider;
			this.inputStream = (inputProvider as WaveStream);
			this.outputFormat = outputFormat;
			this.dmoResampler = new DmoResampler();
			if (!this.dmoResampler.MediaObject.SupportsInputWaveFormat(0, inputProvider.WaveFormat))
			{
				throw new ArgumentException("Unsupported Input Stream format", "inputProvider");
			}
			this.dmoResampler.MediaObject.SetInputWaveFormat(0, inputProvider.WaveFormat);
			if (!this.dmoResampler.MediaObject.SupportsOutputWaveFormat(0, outputFormat))
			{
				throw new ArgumentException("Unsupported Output Stream format", "outputFormat");
			}
			this.dmoResampler.MediaObject.SetOutputWaveFormat(0, outputFormat);
			if (this.inputStream != null)
			{
				this.position = this.InputToOutputPosition(this.inputStream.Position);
			}
			this.inputMediaBuffer = new MediaBuffer(inputProvider.WaveFormat.AverageBytesPerSecond);
			this.outputBuffer = new DmoOutputDataBuffer(outputFormat.AverageBytesPerSecond);
		}

		private long InputToOutputPosition(long inputPosition)
		{
			double num = (double)this.outputFormat.AverageBytesPerSecond / (double)this.inputProvider.WaveFormat.AverageBytesPerSecond;
			long num2 = (long)((double)inputPosition * num);
			if (num2 % (long)this.outputFormat.BlockAlign != 0L)
			{
				num2 -= num2 % (long)this.outputFormat.BlockAlign;
			}
			return num2;
		}

		private long OutputToInputPosition(long outputPosition)
		{
			double num = (double)this.outputFormat.AverageBytesPerSecond / (double)this.inputProvider.WaveFormat.AverageBytesPerSecond;
			long num2 = (long)((double)outputPosition / num);
			if (num2 % (long)this.inputProvider.WaveFormat.BlockAlign != 0L)
			{
				num2 -= num2 % (long)this.inputProvider.WaveFormat.BlockAlign;
			}
			return num2;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int i = 0;
			while (i < count)
			{
				if (this.dmoResampler.MediaObject.IsAcceptingData(0))
				{
					int num = (int)this.OutputToInputPosition((long)(count - i));
					byte[] array = new byte[num];
					int num2 = this.inputProvider.Read(array, 0, num);
					if (num2 == 0)
					{
						break;
					}
					this.inputMediaBuffer.LoadData(array, num2);
					this.dmoResampler.MediaObject.ProcessInput(0, this.inputMediaBuffer, DmoInputDataBufferFlags.None, 0L, 0L);
					this.outputBuffer.MediaBuffer.SetLength(0);
					this.outputBuffer.StatusFlags = DmoOutputDataBufferFlags.None;
					this.dmoResampler.MediaObject.ProcessOutput(DmoProcessOutputFlags.None, 1, new DmoOutputDataBuffer[]
					{
						this.outputBuffer
					});
					if (this.outputBuffer.Length == 0)
					{
						break;
					}
					this.outputBuffer.RetrieveData(buffer, offset + i);
					i += this.outputBuffer.Length;
				}
			}
			this.position += (long)i;
			return i;
		}

		protected override void Dispose(bool disposing)
		{
			if (this.inputMediaBuffer != null)
			{
				this.inputMediaBuffer.Dispose();
				this.inputMediaBuffer = null;
			}
			this.outputBuffer.Dispose();
			if (this.dmoResampler != null)
			{
				this.dmoResampler = null;
			}
			base.Dispose(disposing);
		}
	}
}
