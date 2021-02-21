using System;

namespace SAEA.Audio.Base.NAudio.Codecs
{
	public class G722CodecState
	{
		public bool ItuTestMode
		{
			get;
			set;
		}

		public bool Packed
		{
			get;
			private set;
		}

		public bool EncodeFrom8000Hz
		{
			get;
			private set;
		}

		public int BitsPerSample
		{
			get;
			private set;
		}

		public int[] QmfSignalHistory
		{
			get;
			private set;
		}

		public Band[] Band
		{
			get;
			private set;
		}

		public uint InBuffer
		{
			get;
			internal set;
		}

		public int InBits
		{
			get;
			internal set;
		}

		public uint OutBuffer
		{
			get;
			internal set;
		}

		public int OutBits
		{
			get;
			internal set;
		}

		public G722CodecState(int rate, G722Flags options)
		{
			this.Band = new Band[]
			{
				new Band(),
				new Band()
			};
			this.QmfSignalHistory = new int[24];
			this.ItuTestMode = false;
			if (rate == 48000)
			{
				this.BitsPerSample = 6;
			}
			else if (rate == 56000)
			{
				this.BitsPerSample = 7;
			}
			else
			{
				if (rate != 64000)
				{
					throw new ArgumentException("Invalid rate, should be 48000, 56000 or 64000");
				}
				this.BitsPerSample = 8;
			}
			if ((options & G722Flags.SampleRate8000) == G722Flags.SampleRate8000)
			{
				this.EncodeFrom8000Hz = true;
			}
			if ((options & G722Flags.Packed) == G722Flags.Packed && this.BitsPerSample != 8)
			{
				this.Packed = true;
			}
			else
			{
				this.Packed = false;
			}
			this.Band[0].det = 32;
			this.Band[1].det = 8;
		}
	}
}
