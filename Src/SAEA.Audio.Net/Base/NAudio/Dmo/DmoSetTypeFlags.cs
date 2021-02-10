using System;

namespace SAEA.Audio.NAudio.Dmo
{
	[Flags]
	internal enum DmoSetTypeFlags
	{
		None = 0,
		DMO_SET_TYPEF_TEST_ONLY = 1,
		DMO_SET_TYPEF_CLEAR = 2
	}
}
