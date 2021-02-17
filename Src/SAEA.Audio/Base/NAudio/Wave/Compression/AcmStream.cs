using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	public class AcmStream : IDisposable
	{
		private IntPtr streamHandle;

		private IntPtr driverHandle;

		private AcmStreamHeader streamHeader;

		private readonly WaveFormat sourceFormat;

		public byte[] SourceBuffer
		{
			get
			{
				return this.streamHeader.SourceBuffer;
			}
		}

		public byte[] DestBuffer
		{
			get
			{
				return this.streamHeader.DestBuffer;
			}
		}

		public AcmStream(WaveFormat sourceFormat, WaveFormat destFormat)
		{
			try
			{
				this.streamHandle = IntPtr.Zero;
				this.sourceFormat = sourceFormat;
				int num = Math.Max(65536, sourceFormat.AverageBytesPerSecond);
				num -= num % sourceFormat.BlockAlign;
				MmException.Try(AcmInterop.acmStreamOpen(out this.streamHandle, IntPtr.Zero, sourceFormat, destFormat, null, IntPtr.Zero, IntPtr.Zero, AcmStreamOpenFlags.NonRealTime), "acmStreamOpen");
				int destBufferLength = this.SourceToDest(num);
				this.streamHeader = new AcmStreamHeader(this.streamHandle, num, destBufferLength);
				this.driverHandle = IntPtr.Zero;
			}
			catch
			{
				this.Dispose();
				throw;
			}
		}

		public AcmStream(IntPtr driverId, WaveFormat sourceFormat, WaveFilter waveFilter)
		{
			int num = Math.Max(16384, sourceFormat.AverageBytesPerSecond);
			this.sourceFormat = sourceFormat;
			num -= num % sourceFormat.BlockAlign;
			MmException.Try(AcmInterop.acmDriverOpen(out this.driverHandle, driverId, 0), "acmDriverOpen");
			MmException.Try(AcmInterop.acmStreamOpen(out this.streamHandle, this.driverHandle, sourceFormat, sourceFormat, waveFilter, IntPtr.Zero, IntPtr.Zero, AcmStreamOpenFlags.NonRealTime), "acmStreamOpen");
			this.streamHeader = new AcmStreamHeader(this.streamHandle, num, this.SourceToDest(num));
		}

		public int SourceToDest(int source)
		{
			if (source == 0)
			{
				return 0;
			}
			int result;
			MmException.Try(AcmInterop.acmStreamSize(this.streamHandle, source, out result, AcmStreamSizeFlags.Source), "acmStreamSize");
			return result;
		}

		public int DestToSource(int dest)
		{
			if (dest == 0)
			{
				return 0;
			}
			int result;
			MmException.Try(AcmInterop.acmStreamSize(this.streamHandle, dest, out result, AcmStreamSizeFlags.Destination), "acmStreamSize");
			return result;
		}

		public static WaveFormat SuggestPcmFormat(WaveFormat compressedFormat)
		{
			WaveFormat waveFormat = new WaveFormat(compressedFormat.SampleRate, 16, compressedFormat.Channels);
			MmException.Try(AcmInterop.acmFormatSuggest(IntPtr.Zero, compressedFormat, waveFormat, Marshal.SizeOf(waveFormat), AcmFormatSuggestFlags.FormatTag), "acmFormatSuggest");
			return waveFormat;
		}

		public void Reposition()
		{
			this.streamHeader.Reposition();
		}

		public int Convert(int bytesToConvert, out int sourceBytesConverted)
		{
			if (bytesToConvert % this.sourceFormat.BlockAlign != 0)
			{
				bytesToConvert -= bytesToConvert % this.sourceFormat.BlockAlign;
			}
			return this.streamHeader.Convert(bytesToConvert, out sourceBytesConverted);
		}

		[Obsolete("Call the version returning sourceBytesConverted instead")]
		public int Convert(int bytesToConvert)
		{
			int num;
			int arg_19_0 = this.Convert(bytesToConvert, out num);
			if (num != bytesToConvert)
			{
				throw new MmException(MmResult.NotSupported, "AcmStreamHeader.Convert didn't convert everything");
			}
			return arg_19_0;
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing && this.streamHeader != null)
			{
				this.streamHeader.Dispose();
				this.streamHeader = null;
			}
			if (this.streamHandle != IntPtr.Zero)
			{
				MmResult mmResult = AcmInterop.acmStreamClose(this.streamHandle, 0);
				this.streamHandle = IntPtr.Zero;
				if (mmResult != MmResult.NoError)
				{
					throw new MmException(mmResult, "acmStreamClose");
				}
			}
			if (this.driverHandle != IntPtr.Zero)
			{
				AcmInterop.acmDriverClose(this.driverHandle, 0);
				this.driverHandle = IntPtr.Zero;
			}
		}

		~AcmStream()
		{
			this.Dispose(false);
		}
	}
}
