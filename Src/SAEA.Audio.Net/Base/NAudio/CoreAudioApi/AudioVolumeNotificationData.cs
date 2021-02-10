using System;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
	public class AudioVolumeNotificationData
	{
		private readonly Guid eventContext;

		private readonly bool muted;

		private readonly float masterVolume;

		private readonly int channels;

		private readonly float[] channelVolume;

		private readonly Guid guid;

		public Guid EventContext
		{
			get
			{
				return this.eventContext;
			}
		}

		public bool Muted
		{
			get
			{
				return this.muted;
			}
		}

		public Guid Guid
		{
			get
			{
				return this.guid;
			}
		}

		public float MasterVolume
		{
			get
			{
				return this.masterVolume;
			}
		}

		public int Channels
		{
			get
			{
				return this.channels;
			}
		}

		public float[] ChannelVolume
		{
			get
			{
				return this.channelVolume;
			}
		}

		public AudioVolumeNotificationData(Guid eventContext, bool muted, float masterVolume, float[] channelVolume, Guid guid)
		{
			this.eventContext = eventContext;
			this.muted = muted;
			this.masterVolume = masterVolume;
			this.channels = channelVolume.Length;
			this.channelVolume = channelVolume;
			this.guid = guid;
		}
	}
}
