using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class WaveInEventArgs : EventArgs
	{
		private byte[] buffer;

		private int bytes;

		public byte[] Buffer
		{
			get
			{
				return this.buffer;
			}
		}

		public int BytesRecorded
		{
			get
			{
				return this.bytes;
			}
		}

		public WaveInEventArgs(byte[] buffer, int bytes)
		{
			this.buffer = buffer;
			this.bytes = bytes;
		}
	}
}
