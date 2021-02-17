using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	internal class SampleDataChunk
	{
		private byte[] sampleData;

		public byte[] SampleData
		{
			get
			{
				return this.sampleData;
			}
		}

		public SampleDataChunk(RiffChunk chunk)
		{
			string text = chunk.ReadChunkID();
			if (text != "sdta")
			{
				throw new InvalidDataException(string.Format("Not a sample data chunk ({0})", text));
			}
			this.sampleData = chunk.GetData();
		}
	}
}
