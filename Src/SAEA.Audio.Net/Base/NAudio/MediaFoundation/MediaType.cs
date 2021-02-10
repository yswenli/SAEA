using SAEA.Audio.NAudio.Utils;
using SAEA.Audio.NAudio.Wave;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.MediaFoundation
{
    public class MediaType
	{
		private readonly IMFMediaType mediaType;

		public int SampleRate
		{
			get
			{
				return this.GetUInt32(MediaFoundationAttributes.MF_MT_AUDIO_SAMPLES_PER_SECOND);
			}
			set
			{
				this.mediaType.SetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_SAMPLES_PER_SECOND, value);
			}
		}

		public int ChannelCount
		{
			get
			{
				return this.GetUInt32(MediaFoundationAttributes.MF_MT_AUDIO_NUM_CHANNELS);
			}
			set
			{
				this.mediaType.SetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_NUM_CHANNELS, value);
			}
		}

		public int BitsPerSample
		{
			get
			{
				return this.GetUInt32(MediaFoundationAttributes.MF_MT_AUDIO_BITS_PER_SAMPLE);
			}
			set
			{
				this.mediaType.SetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_BITS_PER_SAMPLE, value);
			}
		}

		public int AverageBytesPerSecond
		{
			get
			{
				return this.GetUInt32(MediaFoundationAttributes.MF_MT_AUDIO_AVG_BYTES_PER_SECOND);
			}
		}

		public Guid SubType
		{
			get
			{
				return this.GetGuid(MediaFoundationAttributes.MF_MT_SUBTYPE);
			}
			set
			{
				this.mediaType.SetGUID(MediaFoundationAttributes.MF_MT_SUBTYPE, value);
			}
		}

		public Guid MajorType
		{
			get
			{
				return this.GetGuid(MediaFoundationAttributes.MF_MT_MAJOR_TYPE);
			}
			set
			{
				this.mediaType.SetGUID(MediaFoundationAttributes.MF_MT_MAJOR_TYPE, value);
			}
		}

		public IMFMediaType MediaFoundationObject
		{
			get
			{
				return this.mediaType;
			}
		}

		public MediaType(IMFMediaType mediaType)
		{
			this.mediaType = mediaType;
		}

		public MediaType()
		{
			this.mediaType = MediaFoundationApi.CreateMediaType();
		}

		public MediaType(WaveFormat waveFormat)
		{
			this.mediaType = MediaFoundationApi.CreateMediaTypeFromWaveFormat(waveFormat);
		}

		private int GetUInt32(Guid key)
		{
			int result;
			this.mediaType.GetUINT32(key, out result);
			return result;
		}

		private Guid GetGuid(Guid key)
		{
			Guid result;
			this.mediaType.GetGUID(key, out result);
			return result;
		}

		public int TryGetUInt32(Guid key, int defaultValue = -1)
		{
			int result = defaultValue;
			try
			{
				this.mediaType.GetUINT32(key, out result);
			}
			catch (COMException exception)
			{
				if (exception.GetHResult() != -1072875802)
				{
					if (exception.GetHResult() == -1072875843)
					{
						throw new ArgumentException("Not a UINT32 parameter");
					}
					throw;
				}
			}
			return result;
		}
	}
}
