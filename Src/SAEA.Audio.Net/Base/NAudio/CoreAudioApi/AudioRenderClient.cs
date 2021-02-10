using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioRenderClient : IDisposable
	{
		private IAudioRenderClient audioRenderClientInterface;

		internal AudioRenderClient(IAudioRenderClient audioRenderClientInterface)
		{
			this.audioRenderClientInterface = audioRenderClientInterface;
		}

		public IntPtr GetBuffer(int numFramesRequested)
		{
			IntPtr result;
			Marshal.ThrowExceptionForHR(this.audioRenderClientInterface.GetBuffer(numFramesRequested, out result));
			return result;
		}

		public void ReleaseBuffer(int numFramesWritten, AudioClientBufferFlags bufferFlags)
		{
			Marshal.ThrowExceptionForHR(this.audioRenderClientInterface.ReleaseBuffer(numFramesWritten, bufferFlags));
		}

		public void Dispose()
		{
			if (this.audioRenderClientInterface != null)
			{
				Marshal.ReleaseComObject(this.audioRenderClientInterface);
				this.audioRenderClientInterface = null;
				GC.SuppressFinalize(this);
			}
		}
	}
}
