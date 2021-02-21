using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class CueWaveFileWriter : WaveFileWriter
	{
		private CueList cues;

		public CueWaveFileWriter(string fileName, WaveFormat waveFormat) : base(fileName, waveFormat)
		{
		}

		public void AddCue(int position, string label)
		{
			if (this.cues == null)
			{
				this.cues = new CueList();
			}
			this.cues.Add(new Cue(position, label));
		}

		private void WriteCues(BinaryWriter w)
		{
			if (this.cues != null)
			{
				int count = this.cues.GetRiffChunks().Length;
				w.Seek(0, SeekOrigin.End);
				if (w.BaseStream.Length % 2L == 1L)
				{
					w.Write(0);
				}
				w.Write(this.cues.GetRiffChunks(), 0, count);
				w.Seek(4, SeekOrigin.Begin);
				w.Write((int)(w.BaseStream.Length - 8L));
			}
		}

		protected override void UpdateHeader(BinaryWriter writer)
		{
			base.UpdateHeader(writer);
			this.WriteCues(writer);
		}
	}
}
