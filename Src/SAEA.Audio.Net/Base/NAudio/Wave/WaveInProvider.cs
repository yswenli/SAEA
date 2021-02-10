using System;

namespace SAEA.Audio.NAudio.Wave
{
	public class WaveInProvider : IWaveProvider
	{
		private IWaveIn waveIn;

		private BufferedWaveProvider bufferedWaveProvider;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveIn.WaveFormat;
			}
		}

		public WaveInProvider(IWaveIn waveIn)
		{
			this.waveIn = waveIn;
			waveIn.DataAvailable += new EventHandler<WaveInEventArgs>(this.waveIn_DataAvailable);
			this.bufferedWaveProvider = new BufferedWaveProvider(this.WaveFormat);
		}

		private void waveIn_DataAvailable(object sender, WaveInEventArgs e)
		{
			this.bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			return this.bufferedWaveProvider.Read(buffer, 0, count);
		}
	}
}
