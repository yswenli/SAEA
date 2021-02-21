using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class SessionCollection
	{
		private readonly IAudioSessionEnumerator audioSessionEnumerator;

		public AudioSessionControl this[int index]
		{
			get
			{
				IAudioSessionControl audioSessionControl;
				Marshal.ThrowExceptionForHR(this.audioSessionEnumerator.GetSession(index, out audioSessionControl));
				return new AudioSessionControl(audioSessionControl);
			}
		}

		public int Count
		{
			get
			{
				int result;
				Marshal.ThrowExceptionForHR(this.audioSessionEnumerator.GetCount(out result));
				return result;
			}
		}

		internal SessionCollection(IAudioSessionEnumerator realEnumerator)
		{
			this.audioSessionEnumerator = realEnumerator;
		}
	}
}
