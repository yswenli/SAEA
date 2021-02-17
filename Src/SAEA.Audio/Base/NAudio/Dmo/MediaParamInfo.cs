using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	internal struct MediaParamInfo
	{
		public MediaParamType mpType;

		public MediaParamCurveType mopCaps;

		public float mpdMinValue;

		public float mpdMaxValue;

		public float mpdNeutralValue;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szUnitText;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szLabel;
	}
}
