using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using SAEA.Audio.NAudio.Utils;
using SAEA.Audio.NAudio.Wave;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioClient : IDisposable
	{
		private IAudioClient audioClientInterface;

		private WaveFormat mixFormat;

		private AudioRenderClient audioRenderClient;

		private AudioCaptureClient audioCaptureClient;

		private AudioClockClient audioClockClient;

		private AudioStreamVolume audioStreamVolume;

		private AudioClientShareMode shareMode;

		public WaveFormat MixFormat
		{
			get
			{
				if (this.mixFormat == null)
				{
					IntPtr intPtr;
					Marshal.ThrowExceptionForHR(this.audioClientInterface.GetMixFormat(out intPtr));
					WaveFormat waveFormat = WaveFormat.MarshalFromPtr(intPtr);
					Marshal.FreeCoTaskMem(intPtr);
					this.mixFormat = waveFormat;
				}
				return this.mixFormat;
			}
		}

		public int BufferSize
		{
			get
			{
				uint result;
				Marshal.ThrowExceptionForHR(this.audioClientInterface.GetBufferSize(out result));
				return (int)result;
			}
		}

		public long StreamLatency
		{
			get
			{
				return this.audioClientInterface.GetStreamLatency();
			}
		}

		public int CurrentPadding
		{
			get
			{
				int result;
				Marshal.ThrowExceptionForHR(this.audioClientInterface.GetCurrentPadding(out result));
				return result;
			}
		}

		public long DefaultDevicePeriod
		{
			get
			{
				long result;
				long num;
				Marshal.ThrowExceptionForHR(this.audioClientInterface.GetDevicePeriod(out result, out num));
				return result;
			}
		}

		public long MinimumDevicePeriod
		{
			get
			{
				long num;
				long result;
				Marshal.ThrowExceptionForHR(this.audioClientInterface.GetDevicePeriod(out num, out result));
				return result;
			}
		}

		public AudioStreamVolume AudioStreamVolume
		{
			get
			{
				if (this.shareMode == AudioClientShareMode.Exclusive)
				{
					throw new InvalidOperationException("AudioStreamVolume is ONLY supported for shared audio streams.");
				}
				if (this.audioStreamVolume == null)
				{
					Guid interfaceId = new Guid("93014887-242D-4068-8A15-CF5E93B90FE3");
					object obj;
					Marshal.ThrowExceptionForHR(this.audioClientInterface.GetService(interfaceId, out obj));
					this.audioStreamVolume = new AudioStreamVolume((IAudioStreamVolume)obj);
				}
				return this.audioStreamVolume;
			}
		}

		public AudioClockClient AudioClockClient
		{
			get
			{
				if (this.audioClockClient == null)
				{
					Guid interfaceId = new Guid("CD63314F-3FBA-4a1b-812C-EF96358728E7");
					object obj;
					Marshal.ThrowExceptionForHR(this.audioClientInterface.GetService(interfaceId, out obj));
					this.audioClockClient = new AudioClockClient((IAudioClock)obj);
				}
				return this.audioClockClient;
			}
		}

		public AudioRenderClient AudioRenderClient
		{
			get
			{
				if (this.audioRenderClient == null)
				{
					Guid interfaceId = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
					object obj;
					Marshal.ThrowExceptionForHR(this.audioClientInterface.GetService(interfaceId, out obj));
					this.audioRenderClient = new AudioRenderClient((IAudioRenderClient)obj);
				}
				return this.audioRenderClient;
			}
		}

		public AudioCaptureClient AudioCaptureClient
		{
			get
			{
				if (this.audioCaptureClient == null)
				{
					Guid interfaceId = new Guid("c8adbd64-e71e-48a0-a4de-185c395cd317");
					object obj;
					Marshal.ThrowExceptionForHR(this.audioClientInterface.GetService(interfaceId, out obj));
					this.audioCaptureClient = new AudioCaptureClient((IAudioCaptureClient)obj);
				}
				return this.audioCaptureClient;
			}
		}

		internal AudioClient(IAudioClient audioClientInterface)
		{
			this.audioClientInterface = audioClientInterface;
		}

		public void Initialize(AudioClientShareMode shareMode, AudioClientStreamFlags streamFlags, long bufferDuration, long periodicity, WaveFormat waveFormat, Guid audioSessionGuid)
		{
			this.shareMode = shareMode;
			Marshal.ThrowExceptionForHR(this.audioClientInterface.Initialize(shareMode, streamFlags, bufferDuration, periodicity, waveFormat, ref audioSessionGuid));
			this.mixFormat = null;
		}

		public bool IsFormatSupported(AudioClientShareMode shareMode, WaveFormat desiredFormat)
		{
			WaveFormatExtensible waveFormatExtensible;
			return this.IsFormatSupported(shareMode, desiredFormat, out waveFormatExtensible);
		}

		private IntPtr GetPointerToPointer()
		{
			return Marshal.AllocHGlobal(MarshalHelpers.SizeOf<IntPtr>());
		}

		public bool IsFormatSupported(AudioClientShareMode shareMode, WaveFormat desiredFormat, out WaveFormatExtensible closestMatchFormat)
		{
			IntPtr pointerToPointer = this.GetPointerToPointer();
			closestMatchFormat = null;
			int num = this.audioClientInterface.IsFormatSupported(shareMode, desiredFormat, pointerToPointer);
			IntPtr intPtr = MarshalHelpers.PtrToStructure<IntPtr>(pointerToPointer);
			if (intPtr != IntPtr.Zero)
			{
				closestMatchFormat = MarshalHelpers.PtrToStructure<WaveFormatExtensible>(intPtr);
				Marshal.FreeCoTaskMem(intPtr);
			}
			Marshal.FreeHGlobal(pointerToPointer);
			if (num == 0)
			{
				return true;
			}
			if (num == 1)
			{
				return false;
			}
			if (num == -2004287480)
			{
				return shareMode != AudioClientShareMode.Exclusive;
			}
			Marshal.ThrowExceptionForHR(num);
			throw new NotSupportedException("Unknown hresult " + num);
		}

		public void Start()
		{
			this.audioClientInterface.Start();
		}

		public void Stop()
		{
			this.audioClientInterface.Stop();
		}

		public void SetEventHandle(IntPtr eventWaitHandle)
		{
			this.audioClientInterface.SetEventHandle(eventWaitHandle);
		}

		public void Reset()
		{
			this.audioClientInterface.Reset();
		}

		public void Dispose()
		{
			if (this.audioClientInterface != null)
			{
				if (this.audioClockClient != null)
				{
					this.audioClockClient.Dispose();
					this.audioClockClient = null;
				}
				if (this.audioRenderClient != null)
				{
					this.audioRenderClient.Dispose();
					this.audioRenderClient = null;
				}
				if (this.audioCaptureClient != null)
				{
					this.audioCaptureClient.Dispose();
					this.audioCaptureClient = null;
				}
				if (this.audioStreamVolume != null)
				{
					this.audioStreamVolume.Dispose();
					this.audioStreamVolume = null;
				}
				Marshal.ReleaseComObject(this.audioClientInterface);
				this.audioClientInterface = null;
				GC.SuppressFinalize(this);
			}
		}
	}
}
