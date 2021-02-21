using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	public class SoundFont
	{
		private InfoChunk info;

		private PresetsChunk presetsChunk;

		private SampleDataChunk sampleData;

		public InfoChunk FileInfo
		{
			get
			{
				return this.info;
			}
		}

		public Preset[] Presets
		{
			get
			{
				return this.presetsChunk.Presets;
			}
		}

		public Instrument[] Instruments
		{
			get
			{
				return this.presetsChunk.Instruments;
			}
		}

		public SampleHeader[] SampleHeaders
		{
			get
			{
				return this.presetsChunk.SampleHeaders;
			}
		}

		public byte[] SampleData
		{
			get
			{
				return this.sampleData.SampleData;
			}
		}

		public SoundFont(string fileName) : this(new FileStream(fileName, FileMode.Open, FileAccess.Read))
		{
		}

		public SoundFont(Stream sfFile)
		{
			try
			{
				RiffChunk topLevelChunk = RiffChunk.GetTopLevelChunk(new BinaryReader(sfFile));
				if (!(topLevelChunk.ChunkID == "RIFF"))
				{
					throw new InvalidDataException("Not a RIFF file");
				}
				string text = topLevelChunk.ReadChunkID();
				if (text != "sfbk")
				{
					throw new InvalidDataException(string.Format("Not a SoundFont ({0})", text));
				}
				RiffChunk nextSubChunk = topLevelChunk.GetNextSubChunk();
				if (!(nextSubChunk.ChunkID == "LIST"))
				{
					throw new InvalidDataException(string.Format("Not info list found ({0})", nextSubChunk.ChunkID));
				}
				this.info = new InfoChunk(nextSubChunk);
				RiffChunk nextSubChunk2 = topLevelChunk.GetNextSubChunk();
				this.sampleData = new SampleDataChunk(nextSubChunk2);
				nextSubChunk2 = topLevelChunk.GetNextSubChunk();
				this.presetsChunk = new PresetsChunk(nextSubChunk2);
			}
			finally
			{
				if (sfFile != null)
				{
					((IDisposable)sfFile).Dispose();
				}
			}
		}

		public override string ToString()
		{
			return string.Format("Info Chunk:\r\n{0}\r\nPresets Chunk:\r\n{1}", this.info, this.presetsChunk);
		}
	}
}
