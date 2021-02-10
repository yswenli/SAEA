using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioCaptureClient : IDisposable
	{
		private IAudioCaptureClient audioCaptureClientInterface;

		internal AudioCaptureClient(IAudioCaptureClient audioCaptureClientInterface)
		{
			this.audioCaptureClientInterface = audioCaptureClientInterface;
		}

		public IntPtr GetBuffer(out int numFramesToRead, out AudioClientBufferFlags bufferFlags, out long devicePosition, out long qpcPosition)
		{
			IntPtr result;
			Marshal.ThrowExceptionForHR(this.audioCaptureClientInterface.GetBuffer(out result, out numFramesToRead, out bufferFlags, out devicePosition, out qpcPosition));
			return result;
		}

		public IntPtr GetBuffer(out int numFramesToRead, out AudioClientBufferFlags bufferFlags)
		{
			IntPtr result;
			long num;
			long num2;
			Marshal.ThrowExceptionForHR(this.audioCaptureClientInterface.GetBuffer(out result, out numFramesToRead, out bufferFlags, out num, out num2));
			return result;
		}

		public int GetNextPacketSize()
		{
			int result;
			Marshal.ThrowExceptionForHR(this.audioCaptureClientInterface.GetNextPacketSize(out result));
			return result;
		}

		public void ReleaseBuffer(int numFramesWritten)
		{
			Marshal.ThrowExceptionForHR(this.audioCaptureClientInterface.ReleaseBuffer(numFramesWritten));
		}

		public void Dispose()
		{
			if (this.audioCaptureClientInterface != null)
			{
				Marshal.ReleaseComObject(this.audioCaptureClientInterface);
				this.audioCaptureClientInterface = null;
				GC.SuppressFinalize(this);
			}
		}
	}
}
