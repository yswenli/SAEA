using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class WaveRecorder : IWaveProvider, IDisposable
	{
		private WaveFileWriter writer;

		private IWaveProvider source;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.source.WaveFormat;
			}
		}

		public WaveRecorder(IWaveProvider source, string destination)
		{
			this.source = source;
			this.writer = new WaveFileWriter(destination, source.WaveFormat);
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			int num = this.source.Read(buffer, offset, count);
			this.writer.Write(buffer, offset, num);
			return num;
		}

		public void Dispose()
		{
			if (this.writer != null)
			{
				this.writer.Dispose();
				this.writer = null;
			}
		}
	}
}
