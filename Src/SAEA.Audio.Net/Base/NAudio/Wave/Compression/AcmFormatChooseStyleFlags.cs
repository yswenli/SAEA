using System;

namespace SAEA.Audio.NAudio.Wave.Compression
{
	[Flags]
	internal enum AcmFormatChooseStyleFlags
	{
		None = 0,
		ShowHelp = 4,
		EnableHook = 8,
		EnableTemplate = 16,
		EnableTemplateHandle = 32,
		InitToWfxStruct = 64,
		ContextHelp = 128
	}
}
