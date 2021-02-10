using SAEA.Audio.NAudio.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAEA.Audio.NAudio.Wave
{
	public class AiffFileReader : WaveStream
	{
		public struct AiffChunk
		{
			public string ChunkName;

			public uint ChunkLength;

			public uint ChunkStart;

			public AiffChunk(uint start, string name, uint length)
			{
				this.ChunkStart = start;
				this.ChunkName = name;
				this.ChunkLength = length + ((length % 2u == 1u) ? 1u : 0u);
			}
		}

		private readonly WaveFormat waveFormat;

		private readonly bool ownInput;

		private readonly long dataPosition;

		private readonly int dataChunkLength;

		private readonly List<AiffFileReader.AiffChunk> chunks = new List<AiffFileReader.AiffChunk>();

		private Stream waveStream;

		private readonly object lockObject = new object();

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
				return (long)this.dataChunkLength;
			}
		}

		public long SampleCount
		{
			get
			{
				if (this.waveFormat.Encoding == WaveFormatEncoding.Pcm || this.waveFormat.Encoding == WaveFormatEncoding.Extensible || this.waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
				{
					return (long)(this.dataChunkLength / this.BlockAlign);
				}
				throw new FormatException("Sample count is calculated only for the standard encodings");
			}
		}

		public override long Position
		{
			get
			{
				return this.waveStream.Position - this.dataPosition;
			}
			set
			{
				object obj = this.lockObject;
				lock (obj)
				{
					value = Math.Min(value, this.Length);
					value -= value % (long)this.waveFormat.BlockAlign;
					this.waveStream.Position = value + this.dataPosition;
				}
			}
		}

		public AiffFileReader(string aiffFile) : this(File.OpenRead(aiffFile))
		{
			this.ownInput = true;
		}

		public AiffFileReader(Stream inputStream)
		{
			this.waveStream = inputStream;
			AiffFileReader.ReadAiffHeader(this.waveStream, out this.waveFormat, out this.dataPosition, out this.dataChunkLength, this.chunks);
			this.Position = 0L;
		}

		public static void ReadAiffHeader(Stream stream, out WaveFormat format, out long dataChunkPosition, out int dataChunkLength, List<AiffFileReader.AiffChunk> chunks)
		{
			dataChunkPosition = -1L;
			format = null;
			BinaryReader binaryReader = new BinaryReader(stream);
			if (AiffFileReader.ReadChunkName(binaryReader) != "FORM")
			{
				throw new FormatException("Not an AIFF file - no FORM header.");
			}
			AiffFileReader.ConvertInt(binaryReader.ReadBytes(4));
			string a = AiffFileReader.ReadChunkName(binaryReader);
			if (a != "AIFC" && a != "AIFF")
			{
				throw new FormatException("Not an AIFF file - no AIFF/AIFC header.");
			}
			dataChunkLength = 0;
			while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
			{
				AiffFileReader.AiffChunk aiffChunk = AiffFileReader.ReadChunkHeader(binaryReader);
				if (aiffChunk.ChunkName == "\0\0\0\0" || binaryReader.BaseStream.Position + (long)((ulong)aiffChunk.ChunkLength) > binaryReader.BaseStream.Length)
				{
					break;
				}
				if (aiffChunk.ChunkName == "COMM")
				{
					short channels = AiffFileReader.ConvertShort(binaryReader.ReadBytes(2));
					AiffFileReader.ConvertInt(binaryReader.ReadBytes(4));
					short bits = AiffFileReader.ConvertShort(binaryReader.ReadBytes(2));
					double num = IEEE.ConvertFromIeeeExtended(binaryReader.ReadBytes(10));
					format = new WaveFormat((int)num, (int)bits, (int)channels);
					if (aiffChunk.ChunkLength > 18u && a == "AIFC")
					{
						if (new string(binaryReader.ReadChars(4)).ToLower() != "none")
						{
							throw new FormatException("Compressed AIFC is not supported.");
						}
						binaryReader.ReadBytes((int)(aiffChunk.ChunkLength - 22u));
					}
					else
					{
						binaryReader.ReadBytes((int)(aiffChunk.ChunkLength - 18u));
					}
				}
				else if (aiffChunk.ChunkName == "SSND")
				{
					uint num2 = AiffFileReader.ConvertInt(binaryReader.ReadBytes(4));
					AiffFileReader.ConvertInt(binaryReader.ReadBytes(4));
					dataChunkPosition = (long)((ulong)(aiffChunk.ChunkStart + 16u + num2));
					dataChunkLength = (int)(aiffChunk.ChunkLength - 8u);
					binaryReader.BaseStream.Position += (long)((ulong)(aiffChunk.ChunkLength - 8u));
				}
				else
				{
					if (chunks != null)
					{
						chunks.Add(aiffChunk);
					}
					binaryReader.BaseStream.Position += (long)((ulong)aiffChunk.ChunkLength);
				}
			}
			if (format == null)
			{
				throw new FormatException("Invalid AIFF file - No COMM chunk found.");
			}
			if (dataChunkPosition == -1L)
			{
				throw new FormatException("Invalid AIFF file - No SSND chunk found.");
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.waveStream != null)
			{
				if (this.ownInput)
				{
					this.waveStream.Close();
				}
				this.waveStream = null;
			}
			base.Dispose(disposing);
		}

		public override int Read(byte[] array, int offset, int count)
		{
			if (count % this.waveFormat.BlockAlign != 0)
			{
				throw new ArgumentException(string.Format("Must read complete blocks: requested {0}, block align is {1}", count, this.WaveFormat.BlockAlign));
			}
			object obj = this.lockObject;
			int result;
			lock (obj)
			{
				if (this.Position + (long)count > (long)this.dataChunkLength)
				{
					count = this.dataChunkLength - (int)this.Position;
				}
				byte[] array2 = new byte[count];
				int num = this.waveStream.Read(array2, offset, count);
				int num2 = this.WaveFormat.BitsPerSample / 8;
				for (int i = 0; i < num; i += num2)
				{
					if (this.WaveFormat.BitsPerSample == 8)
					{
						array[i] = array2[i];
					}
					else if (this.WaveFormat.BitsPerSample == 16)
					{
						array[i] = array2[i + 1];
						array[i + 1] = array2[i];
					}
					else if (this.WaveFormat.BitsPerSample == 24)
					{
						array[i] = array2[i + 2];
						array[i + 1] = array2[i + 1];
						array[i + 2] = array2[i];
					}
					else
					{
						if (this.WaveFormat.BitsPerSample != 32)
						{
							throw new FormatException("Unsupported PCM format.");
						}
						array[i] = array2[i + 3];
						array[i + 1] = array2[i + 2];
						array[i + 2] = array2[i + 1];
						array[i + 3] = array2[i];
					}
				}
				result = num;
			}
			return result;
		}

		private static uint ConvertInt(byte[] buffer)
		{
			if (buffer.Length != 4)
			{
				throw new Exception("Incorrect length for long.");
			}
			return (uint)((int)buffer[0] << 24 | (int)buffer[1] << 16 | (int)buffer[2] << 8 | (int)buffer[3]);
		}

		private static short ConvertShort(byte[] buffer)
		{
			if (buffer.Length != 2)
			{
				throw new Exception("Incorrect length for int.");
			}
			return (short)((int)buffer[0] << 8 | (int)buffer[1]);
		}

		private static AiffFileReader.AiffChunk ReadChunkHeader(BinaryReader br)
		{
			return new AiffFileReader.AiffChunk((uint)br.BaseStream.Position, AiffFileReader.ReadChunkName(br), AiffFileReader.ConvertInt(br.ReadBytes(4)));
		}

		private static string ReadChunkName(BinaryReader br)
		{
			return new string(br.ReadChars(4));
		}
	}
}
