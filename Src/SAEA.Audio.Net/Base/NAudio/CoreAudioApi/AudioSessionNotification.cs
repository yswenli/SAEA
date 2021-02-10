using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    internal class AudioSessionNotification : IAudioSessionNotification
	{
		private AudioSessionManager parent;

		internal AudioSessionNotification(AudioSessionManager parent)
		{
			this.parent = parent;
		}

		[PreserveSig]
		public int OnSessionCreated(IAudioSessionControl newSession)
		{
			this.parent.FireSessionCreated(newSession);
			return 0;
		}
	}
}
