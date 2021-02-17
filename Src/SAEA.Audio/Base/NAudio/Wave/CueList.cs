using SAEA.Audio.Base.NAudio.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class CueList
	{
		private readonly List<Cue> cues = new List<Cue>();

		public int[] CuePositions
		{
			get
			{
				int[] array = new int[this.cues.Count];
				for (int i = 0; i < this.cues.Count; i++)
				{
					array[i] = this.cues[i].Position;
				}
				return array;
			}
		}

		public string[] CueLabels
		{
			get
			{
				string[] array = new string[this.cues.Count];
				for (int i = 0; i < this.cues.Count; i++)
				{
					array[i] = this.cues[i].Label;
				}
				return array;
			}
		}

		public int Count
		{
			get
			{
				return this.cues.Count;
			}
		}

		public Cue this[int index]
		{
			get
			{
				return this.cues[index];
			}
		}

		public CueList()
		{
		}

		public void Add(Cue cue)
		{
			this.cues.Add(cue);
		}

		internal CueList(byte[] cueChunkData, byte[] listChunkData)
		{
			int num = BitConverter.ToInt32(cueChunkData, 0);
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			int[] array = new int[num];
			int num2 = 0;
			int num3 = 4;
			while (cueChunkData.Length - num3 >= 24)
			{
				dictionary[BitConverter.ToInt32(cueChunkData, num3)] = num2;
				array[num2] = BitConverter.ToInt32(cueChunkData, num3 + 20);
				num3 += 24;
				num2++;
			}
			string[] array2 = new string[num];
			int num4 = 0;
			int num5 = ChunkIdentifier.ChunkIdentifierToInt32("labl");
			int num6 = 4;
			while (listChunkData.Length - num6 >= 16)
			{
				if (BitConverter.ToInt32(listChunkData, num6) == num5)
				{
					num4 = BitConverter.ToInt32(listChunkData, num6 + 4) - 4;
					int key = BitConverter.ToInt32(listChunkData, num6 + 8);
					num2 = dictionary[key];
					array2[num2] = Encoding.Default.GetString(listChunkData, num6 + 12, num4 - 1);
				}
				num6 += num4 + num4 % 2 + 12;
			}
			for (int i = 0; i < num; i++)
			{
				this.cues.Add(new Cue(array[i], array2[i]));
			}
		}

		internal byte[] GetRiffChunks()
		{
			if (this.Count == 0)
			{
				return null;
			}
			int num = 12 + 24 * this.Count;
			int num2 = 12;
			for (int i = 0; i < this.Count; i++)
			{
				int num3 = this[i].Label.Length + 1;
				num2 += num3 + num3 % 2 + 12;
			}
			byte[] array = new byte[num + num2];
			int value = ChunkIdentifier.ChunkIdentifierToInt32("cue ");
			int value2 = ChunkIdentifier.ChunkIdentifierToInt32("data");
			int value3 = ChunkIdentifier.ChunkIdentifierToInt32("LIST");
			int value4 = ChunkIdentifier.ChunkIdentifierToInt32("adtl");
			int value5 = ChunkIdentifier.ChunkIdentifierToInt32("labl");
			using (MemoryStream memoryStream = new MemoryStream(array))
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(value);
					binaryWriter.Write(num - 8);
					binaryWriter.Write(this.Count);
					for (int j = 0; j < this.Count; j++)
					{
						int position = this[j].Position;
						binaryWriter.Write(j);
						binaryWriter.Write(position);
						binaryWriter.Write(value2);
						binaryWriter.Seek(8, SeekOrigin.Current);
						binaryWriter.Write(position);
					}
					binaryWriter.Write(value3);
					binaryWriter.Write(num2 - 8);
					binaryWriter.Write(value4);
					for (int k = 0; k < this.Count; k++)
					{
						binaryWriter.Write(value5);
						binaryWriter.Write(this[k].Label.Length + 1 + 4);
						binaryWriter.Write(k);
						binaryWriter.Write(Encoding.Default.GetBytes(this[k].Label.ToCharArray()));
						if (this[k].Label.Length % 2 == 0)
						{
							binaryWriter.Seek(2, SeekOrigin.Current);
						}
						else
						{
							binaryWriter.Seek(1, SeekOrigin.Current);
						}
					}
				}
			}
			return array;
		}

		internal static CueList FromChunks(WaveFileReader reader)
		{
			CueList result = null;
			byte[] array = null;
			byte[] array2 = null;
			foreach (RiffChunk current in reader.ExtraChunks)
			{
				if (current.IdentifierAsString.ToLower() == "cue ")
				{
					array = reader.GetChunkData(current);
				}
				else if (current.IdentifierAsString.ToLower() == "list")
				{
					array2 = reader.GetChunkData(current);
				}
			}
			if (array != null && array2 != null)
			{
				result = new CueList(array, array2);
			}
			return result;
		}
	}
}
