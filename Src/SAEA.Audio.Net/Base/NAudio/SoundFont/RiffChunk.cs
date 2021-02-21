using SAEA.Audio.Base.NAudio.Utils;
using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	internal class RiffChunk
	{
		private string chunkID;

		private uint chunkSize;

		private long dataOffset;

		private BinaryReader riffFile;

		public string ChunkID
		{
			get
			{
				return this.chunkID;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("ChunkID may not be null");
				}
				if (value.Length != 4)
				{
					throw new ArgumentException("ChunkID must be four characters");
				}
				this.chunkID = value;
			}
		}

		public uint ChunkSize
		{
			get
			{
				return this.chunkSize;
			}
		}

		public long DataOffset
		{
			get
			{
				return this.dataOffset;
			}
		}

		public static RiffChunk GetTopLevelChunk(BinaryReader file)
		{
			RiffChunk expr_06 = new RiffChunk(file);
			expr_06.ReadChunk();
			return expr_06;
		}

		private RiffChunk(BinaryReader file)
		{
			this.riffFile = file;
			this.chunkID = "????";
			this.chunkSize = 0u;
			this.dataOffset = 0L;
		}

		public string ReadChunkID()
		{
			byte[] array = this.riffFile.ReadBytes(4);
			if (array.Length != 4)
			{
				throw new InvalidDataException("Couldn't read Chunk ID");
			}
			return ByteEncoding.Instance.GetString(array, 0, array.Length);
		}

		private void ReadChunk()
		{
			this.chunkID = this.ReadChunkID();
			this.chunkSize = this.riffFile.ReadUInt32();
			this.dataOffset = this.riffFile.BaseStream.Position;
		}

		public RiffChunk GetNextSubChunk()
		{
			if (this.riffFile.BaseStream.Position + 8L < this.dataOffset + (long)((ulong)this.chunkSize))
			{
				RiffChunk expr_2E = new RiffChunk(this.riffFile);
				expr_2E.ReadChunk();
				return expr_2E;
			}
			return null;
		}

		public byte[] GetData()
		{
			this.riffFile.BaseStream.Position = this.dataOffset;
			byte[] array = this.riffFile.ReadBytes((int)this.chunkSize);
			if ((long)array.Length != (long)((ulong)this.chunkSize))
			{
				throw new InvalidDataException(string.Format("Couldn't read chunk's data Chunk: {0}, read {1} bytes", this, array.Length));
			}
			return array;
		}

		public string GetDataAsString()
		{
			byte[] data = this.GetData();
			if (data == null)
			{
				return null;
			}
			return ByteEncoding.Instance.GetString(data, 0, data.Length);
		}

		public T GetDataAsStructure<T>(StructureBuilder<T> s)
		{
			this.riffFile.BaseStream.Position = this.dataOffset;
			if ((long)s.Length != (long)((ulong)this.chunkSize))
			{
				throw new InvalidDataException(string.Format("Chunk size is: {0} so can't read structure of: {1}", this.chunkSize, s.Length));
			}
			return s.Read(this.riffFile);
		}

		public T[] GetDataAsStructureArray<T>(StructureBuilder<T> s)
		{
			this.riffFile.BaseStream.Position = this.dataOffset;
			if ((ulong)this.chunkSize % (ulong)((long)s.Length) != 0uL)
			{
				throw new InvalidDataException(string.Format("Chunk size is: {0} not a multiple of structure size: {1}", this.chunkSize, s.Length));
			}
			int num = (int)((ulong)this.chunkSize / (ulong)((long)s.Length));
			T[] array = new T[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = s.Read(this.riffFile);
			}
			return array;
		}

		public override string ToString()
		{
			return string.Format("RiffChunk ID: {0} Size: {1} Data Offset: {2}", this.ChunkID, this.ChunkSize, this.DataOffset);
		}
	}
}
