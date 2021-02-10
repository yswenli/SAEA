using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioEndpointVolumeChannels
	{
		private readonly IAudioEndpointVolume audioEndPointVolume;

		private readonly AudioEndpointVolumeChannel[] channels;

		public int Count
		{
			get
			{
				int result;
				Marshal.ThrowExceptionForHR(this.audioEndPointVolume.GetChannelCount(out result));
				return result;
			}
		}

		public AudioEndpointVolumeChannel this[int index]
		{
			get
			{
				return this.channels[index];
			}
		}

		internal AudioEndpointVolumeChannels(IAudioEndpointVolume parent)
		{
			this.audioEndPointVolume = parent;
			int count = this.Count;
			this.channels = new AudioEndpointVolumeChannel[count];
			for (int i = 0; i < count; i++)
			{
				this.channels[i] = new AudioEndpointVolumeChannel(this.audioEndPointVolume, i);
			}
		}
	}
}
