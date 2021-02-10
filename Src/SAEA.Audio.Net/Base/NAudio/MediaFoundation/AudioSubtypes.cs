using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.MediaFoundation
{
    public static class AudioSubtypes
	{
		[FieldDescription("AAC")]
		public static readonly Guid MFAudioFormat_AAC = new Guid("00001610-0000-0010-8000-00aa00389b71");

		[FieldDescription("ADTS")]
		public static readonly Guid MFAudioFormat_ADTS = new Guid("00001600-0000-0010-8000-00aa00389b71");

		[FieldDescription("Dolby AC3 SPDIF")]
		public static readonly Guid MFAudioFormat_Dolby_AC3_SPDIF = new Guid("00000092-0000-0010-8000-00aa00389b71");

		[FieldDescription("DRM")]
		public static readonly Guid MFAudioFormat_DRM = new Guid("00000009-0000-0010-8000-00aa00389b71");

		[FieldDescription("DTS")]
		public static readonly Guid MFAudioFormat_DTS = new Guid("00000008-0000-0010-8000-00aa00389b71");

		[FieldDescription("IEEE floating-point")]
		public static readonly Guid MFAudioFormat_Float = new Guid("00000003-0000-0010-8000-00aa00389b71");

		[FieldDescription("MP3")]
		public static readonly Guid MFAudioFormat_MP3 = new Guid("00000055-0000-0010-8000-00aa00389b71");

		[FieldDescription("MPEG")]
		public static readonly Guid MFAudioFormat_MPEG = new Guid("00000050-0000-0010-8000-00aa00389b71");

		[FieldDescription("WMA 9 Voice codec")]
		public static readonly Guid MFAudioFormat_MSP1 = new Guid("0000000a-0000-0010-8000-00aa00389b71");

		[FieldDescription("PCM")]
		public static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00aa00389b71");

		[FieldDescription("WMA SPDIF")]
		public static readonly Guid MFAudioFormat_WMASPDIF = new Guid("00000164-0000-0010-8000-00aa00389b71");

		[FieldDescription("WMAudio Lossless")]
		public static readonly Guid MFAudioFormat_WMAudio_Lossless = new Guid("00000163-0000-0010-8000-00aa00389b71");

		[FieldDescription("Windows Media Audio")]
		public static readonly Guid MFAudioFormat_WMAudioV8 = new Guid("00000161-0000-0010-8000-00aa00389b71");

		[FieldDescription("Windows Media Audio Professional")]
		public static readonly Guid MFAudioFormat_WMAudioV9 = new Guid("00000162-0000-0010-8000-00aa00389b71");

		[FieldDescription("Dolby AC3")]
		public static readonly Guid MFAudioFormat_Dolby_AC3 = new Guid("e06d802c-db46-11cf-b4d1-00805f6cbbea");

		[FieldDescription("MPEG-4 and AAC Audio Types")]
		public static readonly Guid MEDIASUBTYPE_RAW_AAC1 = new Guid("000000ff-0000-0010-8000-00aa00389b71");

		[FieldDescription("Dolby Audio Types")]
		public static readonly Guid MEDIASUBTYPE_DVM = new Guid("00002000-0000-0010-8000-00aa00389b71");

		[FieldDescription("Dolby Audio Types")]
		public static readonly Guid MEDIASUBTYPE_DOLBY_DDPLUS = new Guid("a7fb87af-2d02-42fb-a4d4-05cd93843bdd");

		[FieldDescription("μ-law")]
		public static readonly Guid KSDATAFORMAT_SUBTYPE_MULAW = new Guid("00000007-0000-0010-8000-00aa00389b71");

		[FieldDescription("ADPCM")]
		public static readonly Guid KSDATAFORMAT_SUBTYPE_ADPCM = new Guid("00000002-0000-0010-8000-00aa00389b71");

		[FieldDescription("Dolby Digital Plus for HDMI")]
		public static readonly Guid KSDATAFORMAT_SUBTYPE_IEC61937_DOLBY_DIGITAL_PLUS = new Guid("0000000a-0cea-0010-8000-00aa00389b71");

		[FieldDescription("MSAudio1")]
		public static readonly Guid MEDIASUBTYPE_MSAUDIO1 = new Guid("00000160-0000-0010-8000-00aa00389b71");

		[FieldDescription("IMA ADPCM")]
		public static readonly Guid ImaAdpcm = new Guid("00000011-0000-0010-8000-00aa00389b71");

		[FieldDescription("WMSP2")]
		public static readonly Guid WMMEDIASUBTYPE_WMSP2 = new Guid("0000000b-0000-0010-8000-00aa00389b71");
	}
}
