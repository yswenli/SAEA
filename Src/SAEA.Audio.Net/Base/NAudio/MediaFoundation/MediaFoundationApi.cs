using SAEA.Audio.NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SAEA.Audio.NAudio.MediaFoundation
{
    public static class MediaFoundationApi
	{
		private static bool initialized;

		public static void Startup()
		{
			if (!MediaFoundationApi.initialized)
			{
				int num = 2;
				OperatingSystem oSVersion = Environment.OSVersion;
				if (oSVersion.Version.Major == 6 && oSVersion.Version.Minor == 0)
				{
					num = 1;
				}
				MediaFoundationInterop.MFStartup(num << 16 | 112, 0);
				MediaFoundationApi.initialized = true;
			}
		}

		public static IEnumerable<IMFActivate> EnumerateTransforms(Guid category)
		{
			IntPtr intPtr;
			int num;
			MediaFoundationInterop.MFTEnumEx(category, _MFT_ENUM_FLAG.MFT_ENUM_FLAG_ALL, null, null, out intPtr, out num);
			IMFActivate[] array = new IMFActivate[num];
			for (int i = 0; i < num; i++)
			{
				IntPtr pUnk = Marshal.ReadIntPtr(new IntPtr(intPtr.ToInt64() + (long)(i * Marshal.SizeOf(intPtr))));
				array[i] = (IMFActivate)Marshal.GetObjectForIUnknown(pUnk);
			}
			IMFActivate[] array2 = array;
			for (int j = 0; j < array2.Length; j++)
			{
				IMFActivate iMFActivate = array2[j];
				yield return iMFActivate;
			}
			array2 = null;
			Marshal.FreeCoTaskMem(intPtr);
			yield break;
		}

		public static void Shutdown()
		{
			if (MediaFoundationApi.initialized)
			{
				MediaFoundationInterop.MFShutdown();
				MediaFoundationApi.initialized = false;
			}
		}

		public static IMFMediaType CreateMediaType()
		{
			IMFMediaType result;
			MediaFoundationInterop.MFCreateMediaType(out result);
			return result;
		}

		public static IMFMediaType CreateMediaTypeFromWaveFormat(WaveFormat waveFormat)
		{
			IMFMediaType iMFMediaType = MediaFoundationApi.CreateMediaType();
			try
			{
				MediaFoundationInterop.MFInitMediaTypeFromWaveFormatEx(iMFMediaType, waveFormat, Marshal.SizeOf(waveFormat));
			}
			catch (Exception)
			{
				Marshal.ReleaseComObject(iMFMediaType);
				throw;
			}
			return iMFMediaType;
		}

		public static IMFMediaBuffer CreateMemoryBuffer(int bufferSize)
		{
			IMFMediaBuffer result;
			MediaFoundationInterop.MFCreateMemoryBuffer(bufferSize, out result);
			return result;
		}

		public static IMFSample CreateSample()
		{
			IMFSample result;
			MediaFoundationInterop.MFCreateSample(out result);
			return result;
		}

		public static IMFAttributes CreateAttributes(int initialSize)
		{
			IMFAttributes result;
			MediaFoundationInterop.MFCreateAttributes(out result, initialSize);
			return result;
		}

		public static IMFByteStream CreateByteStream(object stream)
		{
			if (stream is IStream)
			{
				IMFByteStream result;
				MediaFoundationInterop.MFCreateMFByteStreamOnStream(stream as IStream, out result);
				return result;
			}
			throw new ArgumentException("Stream must be IStream in desktop apps");
		}

		public static IMFSourceReader CreateSourceReaderFromByteStream(IMFByteStream byteStream)
		{
			IMFSourceReader result;
			MediaFoundationInterop.MFCreateSourceReaderFromByteStream(byteStream, null, out result);
			return result;
		}
	}
}
