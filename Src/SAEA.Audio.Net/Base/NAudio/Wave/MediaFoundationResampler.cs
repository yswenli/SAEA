using SAEA.Audio.Base.NAudio.Dmo;
using SAEA.Audio.Base.NAudio.MediaFoundation;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class MediaFoundationResampler : MediaFoundationTransform
	{
		private int resamplerQuality;

		private static readonly Guid ResamplerClsid = new Guid("f447b69e-1884-4a7e-8055-346f74d6edb3");

		private static readonly Guid IMFTransformIid = new Guid("bf94c121-5b05-4e6f-8000-ba598961414d");

		private IMFActivate activate;

		public int ResamplerQuality
		{
			get
			{
				return this.resamplerQuality;
			}
			set
			{
				if (value < 1 || value > 60)
				{
					throw new ArgumentOutOfRangeException("Resampler Quality must be between 1 and 60");
				}
				this.resamplerQuality = value;
			}
		}

		private static bool IsPcmOrIeeeFloat(WaveFormat waveFormat)
		{
			WaveFormatExtensible waveFormatExtensible = waveFormat as WaveFormatExtensible;
			return waveFormat.Encoding == WaveFormatEncoding.Pcm || waveFormat.Encoding == WaveFormatEncoding.IeeeFloat || (waveFormatExtensible != null && (waveFormatExtensible.SubFormat == AudioSubtypes.MFAudioFormat_PCM || waveFormatExtensible.SubFormat == AudioSubtypes.MFAudioFormat_Float));
		}

		public MediaFoundationResampler(IWaveProvider sourceProvider, WaveFormat outputFormat) : base(sourceProvider, outputFormat)
		{
			if (!MediaFoundationResampler.IsPcmOrIeeeFloat(sourceProvider.WaveFormat))
			{
				throw new ArgumentException("Input must be PCM or IEEE float", "sourceProvider");
			}
			if (!MediaFoundationResampler.IsPcmOrIeeeFloat(outputFormat))
			{
				throw new ArgumentException("Output must be PCM or IEEE float", "outputFormat");
			}
			MediaFoundationApi.Startup();
			this.ResamplerQuality = 60;
			object comObject = this.CreateResamplerComObject();
			this.FreeComObject(comObject);
		}

		private void FreeComObject(object comObject)
		{
			if (this.activate != null)
			{
				this.activate.ShutdownObject();
			}
			Marshal.ReleaseComObject(comObject);
		}

		private object CreateResamplerComObject()
		{
			return new ResamplerMediaComObject();
		}

		private object CreateResamplerComObjectUsingActivator()
		{
			foreach (IMFActivate current in MediaFoundationApi.EnumerateTransforms(MediaFoundationTransformCategories.AudioEffect))
			{
				Guid guid;
				current.GetGUID(MediaFoundationAttributes.MFT_TRANSFORM_CLSID_Attribute, out guid);
				if (guid.Equals(MediaFoundationResampler.ResamplerClsid))
				{
					object result;
					current.ActivateObject(MediaFoundationResampler.IMFTransformIid, out result);
					this.activate = current;
					return result;
				}
			}
			return null;
		}

		public MediaFoundationResampler(IWaveProvider sourceProvider, int outputSampleRate) : this(sourceProvider, MediaFoundationResampler.CreateOutputFormat(sourceProvider.WaveFormat, outputSampleRate))
		{
		}

		protected override IMFTransform CreateTransform()
		{
			object obj = this.CreateResamplerComObject();
			IMFTransform arg_1E_0 = (IMFTransform)obj;
			IMFMediaType iMFMediaType = MediaFoundationApi.CreateMediaTypeFromWaveFormat(this.sourceProvider.WaveFormat);
			arg_1E_0.SetInputType(0, iMFMediaType, _MFT_SET_TYPE_FLAGS.None);
			Marshal.ReleaseComObject(iMFMediaType);
			IMFMediaType iMFMediaType2 = MediaFoundationApi.CreateMediaTypeFromWaveFormat(this.outputWaveFormat);
			arg_1E_0.SetOutputType(0, iMFMediaType2, _MFT_SET_TYPE_FLAGS.None);
			Marshal.ReleaseComObject(iMFMediaType2);
			((IWMResamplerProps)obj).SetHalfFilterLength(this.ResamplerQuality);
			return arg_1E_0;
		}

		private static WaveFormat CreateOutputFormat(WaveFormat inputFormat, int outputSampleRate)
		{
			WaveFormat result;
			if (inputFormat.Encoding == WaveFormatEncoding.Pcm)
			{
				result = new WaveFormat(outputSampleRate, inputFormat.BitsPerSample, inputFormat.Channels);
			}
			else
			{
				if (inputFormat.Encoding != WaveFormatEncoding.IeeeFloat)
				{
					throw new ArgumentException("Can only resample PCM or IEEE float");
				}
				result = WaveFormat.CreateIeeeFloatWaveFormat(outputSampleRate, inputFormat.Channels);
			}
			return result;
		}

		protected override void Dispose(bool disposing)
		{
			if (this.activate != null)
			{
				this.activate.ShutdownObject();
				this.activate = null;
			}
			base.Dispose(disposing);
		}
	}
}
