using SAEA.Audio.Base.NAudio.Wave;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Dmo
{
    public struct DmoMediaType
	{
		private Guid majortype;

		private Guid subtype;

		private bool bFixedSizeSamples;

		private bool bTemporalCompression;

		private int lSampleSize;

		private Guid formattype;

		private IntPtr pUnk;

		private int cbFormat;

		private IntPtr pbFormat;

		public Guid MajorType
		{
			get
			{
				return this.majortype;
			}
		}

		public string MajorTypeName
		{
			get
			{
				return MediaTypes.GetMediaTypeName(this.majortype);
			}
		}

		public Guid SubType
		{
			get
			{
				return this.subtype;
			}
		}

		public string SubTypeName
		{
			get
			{
				if (this.majortype == MediaTypes.MEDIATYPE_Audio)
				{
					return AudioMediaSubtypes.GetAudioSubtypeName(this.subtype);
				}
				return this.subtype.ToString();
			}
		}

		public bool FixedSizeSamples
		{
			get
			{
				return this.bFixedSizeSamples;
			}
		}

		public int SampleSize
		{
			get
			{
				return this.lSampleSize;
			}
		}

		public Guid FormatType
		{
			get
			{
				return this.formattype;
			}
		}

		public string FormatTypeName
		{
			get
			{
				if (this.formattype == DmoMediaTypeGuids.FORMAT_None)
				{
					return "None";
				}
				if (this.formattype == Guid.Empty)
				{
					return "Null";
				}
				if (this.formattype == DmoMediaTypeGuids.FORMAT_WaveFormatEx)
				{
					return "WaveFormatEx";
				}
				return this.FormatType.ToString();
			}
		}

		public WaveFormat GetWaveFormat()
		{
			if (this.formattype == DmoMediaTypeGuids.FORMAT_WaveFormatEx)
			{
				return WaveFormat.MarshalFromPtr(this.pbFormat);
			}
			throw new InvalidOperationException("Not a WaveFormat type");
		}

		public void SetWaveFormat(WaveFormat waveFormat)
		{
			this.majortype = MediaTypes.MEDIATYPE_Audio;
			WaveFormatExtensible waveFormatExtensible = waveFormat as WaveFormatExtensible;
			if (waveFormatExtensible != null)
			{
				this.subtype = waveFormatExtensible.SubFormat;
			}
			else
			{
				WaveFormatEncoding encoding = waveFormat.Encoding;
				if (encoding != WaveFormatEncoding.Pcm)
				{
					if (encoding != WaveFormatEncoding.IeeeFloat)
					{
						if (encoding != WaveFormatEncoding.MpegLayer3)
						{
							throw new ArgumentException(string.Format("Not a supported encoding {0}", waveFormat.Encoding));
						}
						this.subtype = AudioMediaSubtypes.WMMEDIASUBTYPE_MP3;
					}
					else
					{
						this.subtype = AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT;
					}
				}
				else
				{
					this.subtype = AudioMediaSubtypes.MEDIASUBTYPE_PCM;
				}
			}
			this.bFixedSizeSamples = (this.SubType == AudioMediaSubtypes.MEDIASUBTYPE_PCM || this.SubType == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);
			this.formattype = DmoMediaTypeGuids.FORMAT_WaveFormatEx;
			if (this.cbFormat < Marshal.SizeOf(waveFormat))
			{
				throw new InvalidOperationException("Not enough memory assigned for a WaveFormat structure");
			}
			Marshal.StructureToPtr(waveFormat, this.pbFormat, false);
		}
	}
}
