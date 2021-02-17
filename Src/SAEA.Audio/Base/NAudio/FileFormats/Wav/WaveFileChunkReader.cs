using SAEA.Audio.Base.NAudio.SoundFont;
using SAEA.Audio.Base.NAudio.Utils;
using SAEA.Audio.Base.NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using RiffChunk = SAEA.Audio.Base.NAudio.Wave.RiffChunk;

namespace SAEA.Audio.Base.NAudio.FileFormats.Wav
{
    internal class WaveFileChunkReader
	{
		private WaveFormat waveFormat;

		private long dataChunkPosition;

		private long dataChunkLength;

		private List<RiffChunk> riffChunks;

		private readonly bool strictMode;

		private bool isRf64;

		private readonly bool storeAllChunks;

		private long riffSize;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public long DataChunkPosition
		{
			get
			{
				return this.dataChunkPosition;
			}
		}

		public long DataChunkLength
		{
			get
			{
				return this.dataChunkLength;
			}
		}

		public List<RiffChunk> RiffChunks
		{
			get
			{
				return this.riffChunks;
			}
		}

		public WaveFileChunkReader()
		{
			this.storeAllChunks = true;
			this.strictMode = false;
		}

		public void ReadWaveHeader(Stream stream)
		{
			this.dataChunkPosition = -1L;
			this.waveFormat = null;
			this.riffChunks = new List<RiffChunk>();
			this.dataChunkLength = 0L;
			BinaryReader binaryReader = new BinaryReader(stream);
			this.ReadRiffHeader(binaryReader);
			this.riffSize = (long)((ulong)binaryReader.ReadUInt32());
			if (binaryReader.ReadInt32() != ChunkIdentifier.ChunkIdentifierToInt32("WAVE"))
			{
				throw new FormatException("Not a WAVE file - no WAVE header");
			}
			if (this.isRf64)
			{
				this.ReadDs64Chunk(binaryReader);
			}
			int num = ChunkIdentifier.ChunkIdentifierToInt32("data");
			int num2 = ChunkIdentifier.ChunkIdentifierToInt32("fmt ");
			long num3 = Math.Min(this.riffSize + 8L, stream.Length);
			while (stream.Position <= num3 - 8L)
			{
				int num4 = binaryReader.ReadInt32();
				uint num5 = binaryReader.ReadUInt32();
				if (num4 == num)
				{
					this.dataChunkPosition = stream.Position;
					if (!this.isRf64)
					{
						this.dataChunkLength = (long)((ulong)num5);
					}
					stream.Position += (long)((ulong)num5);
				}
				else if (num4 == num2)
				{
					if (num5 > 2147483647u)
					{
						throw new InvalidDataException(string.Format("Format chunk length must be between 0 and {0}.", 2147483647));
					}
					this.waveFormat = WaveFormat.FromFormatChunk(binaryReader, (int)num5);
				}
				else if ((ulong)num5 > (ulong)(stream.Length - stream.Position))
				{
					if (this.strictMode)
					{
						break;
					}
					break;
				}
				else
				{
					if (this.storeAllChunks)
					{
						if (num5 > 2147483647u)
						{
							throw new InvalidDataException(string.Format("RiffChunk chunk length must be between 0 and {0}.", 2147483647));
						}
						this.riffChunks.Add(WaveFileChunkReader.GetRiffChunk(stream, num4, (int)num5));
					}
					stream.Position += (long)((ulong)num5);
				}
			}
			if (this.waveFormat == null)
			{
				throw new FormatException("Invalid WAV file - No fmt chunk found");
			}
			if (this.dataChunkPosition == -1L)
			{
				throw new FormatException("Invalid WAV file - No data chunk found");
			}
		}

		private void ReadDs64Chunk(BinaryReader reader)
		{
			int num = ChunkIdentifier.ChunkIdentifierToInt32("ds64");
			if (reader.ReadInt32() != num)
			{
				throw new FormatException("Invalid RF64 WAV file - No ds64 chunk found");
			}
			int num2 = reader.ReadInt32();
			this.riffSize = reader.ReadInt64();
			this.dataChunkLength = reader.ReadInt64();
			reader.ReadInt64();
			reader.ReadBytes(num2 - 24);
		}

		private static RiffChunk GetRiffChunk(Stream stream, int chunkIdentifier, int chunkLength)
		{
			return new RiffChunk(chunkIdentifier, chunkLength, stream.Position);
		}

		private void ReadRiffHeader(BinaryReader br)
		{
			int num = br.ReadInt32();
			if (num == ChunkIdentifier.ChunkIdentifierToInt32("RF64"))
			{
				this.isRf64 = true;
				return;
			}
			if (num != ChunkIdentifier.ChunkIdentifierToInt32("RIFF"))
			{
				throw new FormatException("Not a WAVE file - no RIFF header");
			}
		}
	}
}
