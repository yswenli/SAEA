using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class SampleEventArgs : EventArgs
	{
		public float Left
		{
			get;
			set;
		}

		public float Right
		{
			get;
			set;
		}

		public SampleEventArgs(float left, float right)
		{
			this.Left = left;
			this.Right = right;
		}
	}
}
