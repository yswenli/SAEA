using System;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	internal struct Blob
	{
		public int Length;

		public IntPtr Data;

		private void FixCS0649()
		{
			this.Length = 0;
			this.Data = IntPtr.Zero;
		}
	}
}
