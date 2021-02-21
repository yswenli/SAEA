using SAEA.Audio.Base.NAudio.Wave.Compression;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class AcmMp3FrameDecompressor : IMp3FrameDecompressor, IDisposable
	{
		private readonly AcmStream conversionStream;

		private readonly WaveFormat pcmFormat;

		private bool disposed;

		public WaveFormat OutputFormat
		{
			get
			{
				return this.pcmFormat;
			}
		}

		public AcmMp3FrameDecompressor(WaveFormat sourceFormat)
		{
			this.pcmFormat = AcmStream.SuggestPcmFormat(sourceFormat);
			try
			{
				this.conversionStream = new AcmStream(sourceFormat, this.pcmFormat);
			}
			catch (Exception)
			{
				this.disposed = true;
				GC.SuppressFinalize(this);
				throw;
			}
		}

		public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
		{
			if (frame == null)
			{
				throw new ArgumentNullException("frame", "You must provide a non-null Mp3Frame to decompress");
			}
			Array.Copy(frame.RawData, this.conversionStream.SourceBuffer, frame.FrameLength);
			int num = 0;
			int num2 = this.conversionStream.Convert(frame.FrameLength, out num);
			if (num != frame.FrameLength)
			{
				throw new InvalidOperationException(string.Format("Couldn't convert the whole MP3 frame (converted {0}/{1})", num, frame.FrameLength));
			}
			Array.Copy(this.conversionStream.DestBuffer, 0, dest, destOffset, num2);
			return num2;
		}

		public void Reset()
		{
			this.conversionStream.Reposition();
		}

		public void Dispose()
		{
			if (!this.disposed)
			{
				this.disposed = true;
				if (this.conversionStream != null)
				{
					this.conversionStream.Dispose();
				}
				GC.SuppressFinalize(this);
			}
		}

		~AcmMp3FrameDecompressor()
		{
			this.Dispose();
		}
	}
}
