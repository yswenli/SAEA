using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
    public static class MediaTypes
	{
		public static readonly Guid MFMediaType_Default = new Guid("81A412E6-8103-4B06-857F-1862781024AC");

		[FieldDescription("Audio")]
		public static readonly Guid MFMediaType_Audio = new Guid("73647561-0000-0010-8000-00aa00389b71");

		[FieldDescription("Video")]
		public static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00aa00389b71");

		[FieldDescription("Protected Media")]
		public static readonly Guid MFMediaType_Protected = new Guid("7b4b6fe6-9d04-4494-be14-7e0bd076c8e4");

		[FieldDescription("SAMI captions")]
		public static readonly Guid MFMediaType_SAMI = new Guid("e69669a0-3dcd-40cb-9e2e-3708387c0616");

		[FieldDescription("Script stream")]
		public static readonly Guid MFMediaType_Script = new Guid("72178c22-e45b-11d5-bc2a-00b0d0f3f4ab");

		[FieldDescription("Still image stream")]
		public static readonly Guid MFMediaType_Image = new Guid("72178c23-e45b-11d5-bc2a-00b0d0f3f4ab");

		[FieldDescription("HTML stream")]
		public static readonly Guid MFMediaType_HTML = new Guid("72178c24-e45b-11d5-bc2a-00b0d0f3f4ab");

		[FieldDescription("Binary stream")]
		public static readonly Guid MFMediaType_Binary = new Guid("72178c25-e45b-11d5-bc2a-00b0d0f3f4ab");

		[FieldDescription("File transfer")]
		public static readonly Guid MFMediaType_FileTransfer = new Guid("72178c26-e45b-11d5-bc2a-00b0d0f3f4ab");
	}
}
