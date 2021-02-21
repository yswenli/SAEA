using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class RawSourceWaveStream : WaveStream
	{
		private readonly Stream sourceStream;

		private readonly WaveFormat waveFormat;

		public override WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public override long Length
		{
			get
			{
				return this.sourceStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return this.sourceStream.Position;
			}
			set
			{
				this.sourceStream.Position = value - value % (long)this.waveFormat.BlockAlign;
			}
		}

		public RawSourceWaveStream(Stream sourceStream, WaveFormat waveFormat)
		{
			this.sourceStream = sourceStream;
			this.waveFormat = waveFormat;
		}

		public RawSourceWaveStream(byte[] byteStream, int offset, int count, WaveFormat waveFormat)
		{
			this.sourceStream = new MemoryStream(byteStream, offset, count);
			this.waveFormat = waveFormat;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int result;
			try
			{
				result = this.sourceStream.Read(buffer, offset, count);
			}
			catch (EndOfStreamException)
			{
				result = 0;
			}
			return result;
		}
	}
}
