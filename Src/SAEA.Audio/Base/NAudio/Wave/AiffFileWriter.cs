using SAEA.Audio.Base.NAudio.Utils;
using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class AiffFileWriter : Stream
	{
		private Stream outStream;

		private BinaryWriter writer;

		private long dataSizePos;

		private long commSampleCountPos;

		private int dataChunkSize = 8;

		private WaveFormat format;

		private string filename;

		private byte[] value24 = new byte[3];

		public string Filename
		{
			get
			{
				return this.filename;
			}
		}

		public override long Length
		{
			get
			{
				return (long)this.dataChunkSize;
			}
		}

		public WaveFormat WaveFormat
		{
			get
			{
				return this.format;
			}
		}

		public override bool CanRead
		{
			get
			{
				return false;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return true;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return false;
			}
		}

		public override long Position
		{
			get
			{
				return (long)this.dataChunkSize;
			}
			set
			{
				throw new InvalidOperationException("Repositioning an AiffFileWriter is not supported");
			}
		}

		public static void CreateAiffFile(string filename, WaveStream sourceProvider)
		{
			using (AiffFileWriter aiffFileWriter = new AiffFileWriter(filename, sourceProvider.WaveFormat))
			{
				byte[] array = new byte[16384];
				while (sourceProvider.Position < sourceProvider.Length)
				{
					int count = Math.Min((int)(sourceProvider.Length - sourceProvider.Position), array.Length);
					int num = sourceProvider.Read(array, 0, count);
					if (num == 0)
					{
						break;
					}
					aiffFileWriter.Write(array, 0, num);
				}
			}
		}

		public AiffFileWriter(Stream outStream, WaveFormat format)
		{
			this.outStream = outStream;
			this.format = format;
			this.writer = new BinaryWriter(outStream, Encoding.UTF8);
			this.writer.Write(Encoding.UTF8.GetBytes("FORM"));
			this.writer.Write(0);
			this.writer.Write(Encoding.UTF8.GetBytes("AIFF"));
			this.CreateCommChunk();
			this.WriteSsndChunkHeader();
		}

		public AiffFileWriter(string filename, WaveFormat format) : this(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read), format)
		{
			this.filename = filename;
		}

		private void WriteSsndChunkHeader()
		{
			this.writer.Write(Encoding.UTF8.GetBytes("SSND"));
			this.dataSizePos = this.outStream.Position;
			this.writer.Write(0);
			this.writer.Write(0);
			this.writer.Write(this.SwapEndian(this.format.BlockAlign));
		}

		private byte[] SwapEndian(short n)
		{
			return new byte[]
			{
				(byte)(n >> 8),
				(byte)(n & 255)
			};
		}

		private byte[] SwapEndian(int n)
		{
			return new byte[]
			{
				(byte)(n >> 24 & 255),
				(byte)(n >> 16 & 255),
				(byte)(n >> 8 & 255),
				(byte)(n & 255)
			};
		}

		private void CreateCommChunk()
		{
			this.writer.Write(Encoding.UTF8.GetBytes("COMM"));
			this.writer.Write(this.SwapEndian(18));
			this.writer.Write(this.SwapEndian((short)this.format.Channels));
			this.commSampleCountPos = this.outStream.Position;
			this.writer.Write(0);
			this.writer.Write(this.SwapEndian((short)this.format.BitsPerSample));
			this.writer.Write(IEEE.ConvertToIeeeExtended((double)this.format.SampleRate));
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new InvalidOperationException("Cannot read from an AiffFileWriter");
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new InvalidOperationException("Cannot seek within an AiffFileWriter");
		}

		public override void SetLength(long value)
		{
			throw new InvalidOperationException("Cannot set length of an AiffFileWriter");
		}

		public override void Write(byte[] data, int offset, int count)
		{
			byte[] array = new byte[data.Length];
			int num = this.format.BitsPerSample / 8;
			for (int i = 0; i < data.Length; i++)
			{
				int num2 = (int)Math.Floor((double)i / (double)num) * num + (num - i % num - 1);
				array[i] = data[num2];
			}
			this.outStream.Write(array, offset, count);
			this.dataChunkSize += count;
		}

		public void WriteSample(float sample)
		{
			if (this.WaveFormat.BitsPerSample == 16)
			{
				this.writer.Write(this.SwapEndian((short)(32767f * sample)));
				this.dataChunkSize += 2;
				return;
			}
			if (this.WaveFormat.BitsPerSample == 24)
			{
				byte[] bytes = BitConverter.GetBytes((int)(2.14748365E+09f * sample));
				this.value24[2] = bytes[1];
				this.value24[1] = bytes[2];
				this.value24[0] = bytes[3];
				this.writer.Write(this.value24);
				this.dataChunkSize += 3;
				return;
			}
			if (this.WaveFormat.BitsPerSample == 32 && this.WaveFormat.Encoding == WaveFormatEncoding.Extensible)
			{
				this.writer.Write(this.SwapEndian(65535 * (int)sample));
				this.dataChunkSize += 4;
				return;
			}
			throw new InvalidOperationException("Only 16, 24 or 32 bit PCM or IEEE float audio data supported");
		}

		public void WriteSamples(float[] samples, int offset, int count)
		{
			for (int i = 0; i < count; i++)
			{
				this.WriteSample(samples[offset + i]);
			}
		}

		public void WriteSamples(short[] samples, int offset, int count)
		{
			if (this.WaveFormat.BitsPerSample == 16)
			{
				for (int i = 0; i < count; i++)
				{
					this.writer.Write(this.SwapEndian(samples[i + offset]));
				}
				this.dataChunkSize += count * 2;
				return;
			}
			if (this.WaveFormat.BitsPerSample == 24)
			{
				for (int j = 0; j < count; j++)
				{
					byte[] bytes = BitConverter.GetBytes(65535 * (int)samples[j + offset]);
					this.value24[2] = bytes[1];
					this.value24[1] = bytes[2];
					this.value24[0] = bytes[3];
					this.writer.Write(this.value24);
				}
				this.dataChunkSize += count * 3;
				return;
			}
			if (this.WaveFormat.BitsPerSample == 32 && this.WaveFormat.Encoding == WaveFormatEncoding.Extensible)
			{
				for (int k = 0; k < count; k++)
				{
					this.writer.Write(this.SwapEndian(65535 * (int)samples[k + offset]));
				}
				this.dataChunkSize += count * 4;
				return;
			}
			throw new InvalidOperationException("Only 16, 24 or 32 bit PCM audio data supported");
		}

		public override void Flush()
		{
			this.writer.Flush();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.outStream != null)
			{
				try
				{
					this.UpdateHeader(this.writer);
				}
				finally
				{
					this.outStream.Close();
					this.outStream = null;
				}
			}
		}

		protected virtual void UpdateHeader(BinaryWriter writer)
		{
			this.Flush();
			writer.Seek(4, SeekOrigin.Begin);
			writer.Write(this.SwapEndian((int)(this.outStream.Length - 8L)));
			this.UpdateCommChunk(writer);
			this.UpdateSsndChunk(writer);
		}

		private void UpdateCommChunk(BinaryWriter writer)
		{
			writer.Seek((int)this.commSampleCountPos, SeekOrigin.Begin);
			writer.Write(this.SwapEndian(this.dataChunkSize * 8 / this.format.BitsPerSample / this.format.Channels));
		}

		private void UpdateSsndChunk(BinaryWriter writer)
		{
			writer.Seek((int)this.dataSizePos, SeekOrigin.Begin);
			writer.Write(this.SwapEndian(this.dataChunkSize));
		}

		~AiffFileWriter()
		{
			this.Dispose(false);
		}
	}
}
