using System;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	internal struct AudioVolumeNotificationDataStruct
	{
		public Guid guidEventContext;

		public bool bMuted;

		public float fMasterVolume;

		public uint nChannels;

		public float ChannelVolume;

		private void FixCS0649()
		{
			this.guidEventContext = Guid.Empty;
			this.bMuted = false;
			this.fMasterVolume = 0f;
			this.nChannels = 0u;
			this.ChannelVolume = 0f;
		}
	}
}
