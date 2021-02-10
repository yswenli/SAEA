using System;

namespace SAEA.Audio.NAudio.Wave.Compression
{
	public class AcmFormatTag
	{
		private AcmFormatTagDetails formatTagDetails;

		public int FormatTagIndex
		{
			get
			{
				return this.formatTagDetails.formatTagIndex;
			}
		}

		public WaveFormatEncoding FormatTag
		{
			get
			{
				return (WaveFormatEncoding)this.formatTagDetails.formatTag;
			}
		}

		public int FormatSize
		{
			get
			{
				return this.formatTagDetails.formatSize;
			}
		}

		public AcmDriverDetailsSupportFlags SupportFlags
		{
			get
			{
				return this.formatTagDetails.supportFlags;
			}
		}

		public int StandardFormatsCount
		{
			get
			{
				return this.formatTagDetails.standardFormatsCount;
			}
		}

		public string FormatDescription
		{
			get
			{
				return this.formatTagDetails.formatDescription;
			}
		}

		internal AcmFormatTag(AcmFormatTagDetails formatTagDetails)
		{
			this.formatTagDetails = formatTagDetails;
		}
	}
}
