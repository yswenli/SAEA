using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Audio.Base.NAudio.FileFormats.Map
{
	public class CakewalkMapFile
	{
		private int mapEntryCount;

		private readonly List<CakewalkDrumMapping> drumMappings;

		private MapBlockHeader fileHeader1;

		private MapBlockHeader fileHeader2;

		private MapBlockHeader mapNameHeader;

		private MapBlockHeader outputs1Header;

		private MapBlockHeader outputs2Header;

		private MapBlockHeader outputs3Header;

		private int outputs1Count;

		private int outputs2Count;

		private int outputs3Count;

		private string mapName;

		public List<CakewalkDrumMapping> DrumMappings
		{
			get
			{
				return this.drumMappings;
			}
		}

		public CakewalkMapFile(string filename)
		{
			using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(filename), Encoding.Unicode))
			{
				this.drumMappings = new List<CakewalkDrumMapping>();
				this.ReadMapHeader(binaryReader);
				for (int i = 0; i < this.mapEntryCount; i++)
				{
					this.drumMappings.Add(this.ReadMapEntry(binaryReader));
				}
				this.ReadMapName(binaryReader);
				this.ReadOutputsSection1(binaryReader);
				if (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
				{
					this.ReadOutputsSection2(binaryReader);
					if (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
					{
						this.ReadOutputsSection3(binaryReader);
					}
				}
			}
		}

		private void ReadMapHeader(BinaryReader reader)
		{
			this.fileHeader1 = MapBlockHeader.Read(reader);
			this.fileHeader2 = MapBlockHeader.Read(reader);
			this.mapEntryCount = reader.ReadInt32();
		}

		private CakewalkDrumMapping ReadMapEntry(BinaryReader reader)
		{
			CakewalkDrumMapping cakewalkDrumMapping = new CakewalkDrumMapping();
			reader.ReadInt32();
			cakewalkDrumMapping.InNote = reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			cakewalkDrumMapping.VelocityScale = reader.ReadSingle();
			cakewalkDrumMapping.Channel = reader.ReadInt32();
			cakewalkDrumMapping.OutNote = reader.ReadInt32();
			cakewalkDrumMapping.OutPort = reader.ReadInt32();
			cakewalkDrumMapping.VelocityAdjust = reader.ReadInt32();
			char[] array = reader.ReadChars(32);
			int num = 0;
			while (num < array.Length && array[num] != '\0')
			{
				num++;
			}
			cakewalkDrumMapping.NoteName = new string(array, 0, num);
			return cakewalkDrumMapping;
		}

		private void ReadMapName(BinaryReader reader)
		{
			this.mapNameHeader = MapBlockHeader.Read(reader);
			char[] array = reader.ReadChars(34);
			int num = 0;
			while (num < array.Length && array[num] != '\0')
			{
				num++;
			}
			this.mapName = new string(array, 0, num);
			reader.ReadBytes(98);
		}

		private void ReadOutputsSection1(BinaryReader reader)
		{
			this.outputs1Header = MapBlockHeader.Read(reader);
			this.outputs1Count = reader.ReadInt32();
			for (int i = 0; i < this.outputs1Count; i++)
			{
				reader.ReadBytes(20);
			}
		}

		private void ReadOutputsSection2(BinaryReader reader)
		{
			this.outputs2Header = MapBlockHeader.Read(reader);
			this.outputs2Count = reader.ReadInt32();
			for (int i = 0; i < this.outputs2Count; i++)
			{
				reader.ReadBytes(24);
			}
		}

		private void ReadOutputsSection3(BinaryReader reader)
		{
			this.outputs3Header = MapBlockHeader.Read(reader);
			if (this.outputs3Header.Length > 0)
			{
				this.outputs3Count = reader.ReadInt32();
				for (int i = 0; i < this.outputs3Count; i++)
				{
					reader.ReadBytes(36);
				}
			}
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendFormat("FileHeader1: {0}\r\n", this.fileHeader1);
			stringBuilder.AppendFormat("FileHeader2: {0}\r\n", this.fileHeader2);
			stringBuilder.AppendFormat("MapEntryCount: {0}\r\n", this.mapEntryCount);
			foreach (CakewalkDrumMapping current in this.drumMappings)
			{
				stringBuilder.AppendFormat("   Map: {0}\r\n", current);
			}
			stringBuilder.AppendFormat("MapNameHeader: {0}\r\n", this.mapNameHeader);
			stringBuilder.AppendFormat("MapName: {0}\r\n", this.mapName);
			stringBuilder.AppendFormat("Outputs1Header: {0} Count: {1}\r\n", this.outputs1Header, this.outputs1Count);
			stringBuilder.AppendFormat("Outputs2Header: {0} Count: {1}\r\n", this.outputs2Header, this.outputs2Count);
			stringBuilder.AppendFormat("Outputs3Header: {0} Count: {1}\r\n", this.outputs3Header, this.outputs3Count);
			return stringBuilder.ToString();
		}
	}
}
