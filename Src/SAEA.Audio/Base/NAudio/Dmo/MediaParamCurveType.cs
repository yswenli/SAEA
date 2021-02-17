using System;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	[Flags]
	internal enum MediaParamCurveType
	{
		MP_CURVE_JUMP = 1,
		MP_CURVE_LINEAR = 2,
		MP_CURVE_SQUARE = 4,
		MP_CURVE_INVSQUARE = 8,
		MP_CURVE_SINE = 16
	}
}
