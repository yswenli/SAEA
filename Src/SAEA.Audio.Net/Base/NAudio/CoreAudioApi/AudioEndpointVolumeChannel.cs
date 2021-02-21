using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class AudioEndpointVolumeChannel
	{
		private readonly uint channel;

		private readonly IAudioEndpointVolume audioEndpointVolume;

		private Guid notificationGuid = Guid.Empty;

		public Guid NotificationGuid
		{
			get
			{
				return this.notificationGuid;
			}
			set
			{
				this.notificationGuid = value;
			}
		}

		public float VolumeLevel
		{
			get
			{
				float result;
				Marshal.ThrowExceptionForHR(this.audioEndpointVolume.GetChannelVolumeLevel(this.channel, out result));
				return result;
			}
			set
			{
				Marshal.ThrowExceptionForHR(this.audioEndpointVolume.SetChannelVolumeLevel(this.channel, value, ref this.notificationGuid));
			}
		}

		public float VolumeLevelScalar
		{
			get
			{
				float result;
				Marshal.ThrowExceptionForHR(this.audioEndpointVolume.GetChannelVolumeLevelScalar(this.channel, out result));
				return result;
			}
			set
			{
				Marshal.ThrowExceptionForHR(this.audioEndpointVolume.SetChannelVolumeLevelScalar(this.channel, value, ref this.notificationGuid));
			}
		}

		internal AudioEndpointVolumeChannel(IAudioEndpointVolume parent, int channel)
		{
			this.channel = (uint)channel;
			this.audioEndpointVolume = parent;
		}
	}
}
