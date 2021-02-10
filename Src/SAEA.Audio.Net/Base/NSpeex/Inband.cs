using System;

namespace SAEA.Audio.NSpeex
{
	internal class Inband
	{
		private Stereo stereo;

		public Inband(Stereo stereo)
		{
			this.stereo = stereo;
		}

		public void SpeexInbandRequest(Bits bits)
		{
			switch (bits.Unpack(4))
			{
			case 0:
				bits.Advance(1);
				return;
			case 1:
				bits.Advance(1);
				return;
			case 2:
				bits.Advance(4);
				return;
			case 3:
				bits.Advance(4);
				return;
			case 4:
				bits.Advance(4);
				return;
			case 5:
				bits.Advance(4);
				return;
			case 6:
				bits.Advance(4);
				return;
			case 7:
				bits.Advance(4);
				return;
			case 8:
				bits.Advance(8);
				return;
			case 9:
				this.stereo.Init(bits);
				return;
			case 10:
				bits.Advance(16);
				return;
			case 11:
				bits.Advance(16);
				return;
			case 12:
				bits.Advance(32);
				return;
			case 13:
				bits.Advance(32);
				return;
			case 14:
				bits.Advance(64);
				return;
			case 15:
				bits.Advance(64);
				return;
			default:
				return;
			}
		}

		public void UserInbandRequest(Bits bits)
		{
			int num = bits.Unpack(4);
			bits.Advance(5 + 8 * num);
		}
	}
}
