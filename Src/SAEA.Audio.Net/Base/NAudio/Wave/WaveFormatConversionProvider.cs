using SAEA.Audio.NAudio.Wave.Compression;
using System;

namespace SAEA.Audio.NAudio.Wave
{
    public class WaveFormatConversionProvider : IWaveProvider, IDisposable
	{
		private readonly AcmStream conversionStream;

		private readonly IWaveProvider sourceProvider;

		private readonly int preferredSourceReadSize;

		private int leftoverDestBytes;

		private int leftoverDestOffset;

		private int leftoverSourceBytes;

		private bool isDisposed;

		public WaveFormat WaveFormat
		{
            get;private set;
		}

		public WaveFormatConversionProvider(WaveFormat targetFormat, IWaveProvider sourceProvider)
		{
			this.sourceProvider = sourceProvider;
			this.WaveFormat= targetFormat;
			this.conversionStream = new AcmStream(sourceProvider.WaveFormat, targetFormat);
			this.preferredSourceReadSize = Math.Min(sourceProvider.WaveFormat.AverageBytesPerSecond, this.conversionStream.SourceBuffer.Length);
			this.preferredSourceReadSize -= this.preferredSourceReadSize % sourceProvider.WaveFormat.BlockAlign;
		}

		public void Reposition()
		{
			this.leftoverDestBytes = 0;
			this.leftoverDestOffset = 0;
			this.leftoverSourceBytes = 0;
			this.conversionStream.Reposition();
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			int i = 0;
			if (count % this.WaveFormat.BlockAlign != 0)
			{
				count -= count % this.WaveFormat.BlockAlign;
			}
			while (i < count)
			{
				int num = Math.Min(count - i, this.leftoverDestBytes);
				if (num > 0)
				{
					Array.Copy(this.conversionStream.DestBuffer, this.leftoverDestOffset, buffer, offset + i, num);
					this.leftoverDestOffset += num;
					this.leftoverDestBytes -= num;
					i += num;
				}
				if (i >= count)
				{
					break;
				}
				int count2 = Math.Min(this.preferredSourceReadSize, this.conversionStream.SourceBuffer.Length - this.leftoverSourceBytes);
				int num2 = this.sourceProvider.Read(this.conversionStream.SourceBuffer, this.leftoverSourceBytes, count2) + this.leftoverSourceBytes;
				if (num2 == 0)
				{
					break;
				}
				int num4;
				int num3 = this.conversionStream.Convert(num2, out num4);
				if (num4 == 0)
				{
					break;
				}
				this.leftoverSourceBytes = num2 - num4;
				if (this.leftoverSourceBytes > 0)
				{
					Buffer.BlockCopy(this.conversionStream.SourceBuffer, num4, this.conversionStream.SourceBuffer, 0, this.leftoverSourceBytes);
				}
				if (num3 <= 0)
				{
					break;
				}
				int val = count - i;
				int num5 = Math.Min(num3, val);
				if (num5 < num3)
				{
					this.leftoverDestBytes = num3 - num5;
					this.leftoverDestOffset = num5;
				}
				Array.Copy(this.conversionStream.DestBuffer, 0, buffer, i + offset, num5);
				i += num5;
			}
			return i;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!this.isDisposed)
			{
				this.isDisposed = true;
				AcmStream expr_15 = this.conversionStream;
				if (expr_15 == null)
				{
					return;
				}
				expr_15.Dispose();
			}
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this.Dispose(true);
		}

		~WaveFormatConversionProvider()
		{
			this.Dispose(false);
		}
	}
}
