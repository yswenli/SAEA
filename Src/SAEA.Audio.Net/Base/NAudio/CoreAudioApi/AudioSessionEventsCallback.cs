using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioSessionEventsCallback : IAudioSessionEvents
	{
		private readonly IAudioSessionEventsHandler audioSessionEventsHandler;

		public AudioSessionEventsCallback(IAudioSessionEventsHandler handler)
		{
			this.audioSessionEventsHandler = handler;
		}

		public int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] [In] string displayName, [In] ref Guid eventContext)
		{
			this.audioSessionEventsHandler.OnDisplayNameChanged(displayName);
			return 0;
		}

		public int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] [In] string iconPath, [In] ref Guid eventContext)
		{
			this.audioSessionEventsHandler.OnIconPathChanged(iconPath);
			return 0;
		}

		public int OnSimpleVolumeChanged([MarshalAs(UnmanagedType.R4)] [In] float volume, [MarshalAs(UnmanagedType.Bool)] [In] bool isMuted, [In] ref Guid eventContext)
		{
			this.audioSessionEventsHandler.OnVolumeChanged(volume, isMuted);
			return 0;
		}

		public int OnChannelVolumeChanged([MarshalAs(UnmanagedType.U4)] [In] uint channelCount, [MarshalAs(UnmanagedType.SysInt)] [In] IntPtr newVolumes, [MarshalAs(UnmanagedType.U4)] [In] uint channelIndex, [In] ref Guid eventContext)
		{
			this.audioSessionEventsHandler.OnChannelVolumeChanged(channelCount, newVolumes, channelIndex);
			return 0;
		}

		public int OnGroupingParamChanged([In] ref Guid groupingId, [In] ref Guid eventContext)
		{
			this.audioSessionEventsHandler.OnGroupingParamChanged(ref groupingId);
			return 0;
		}

		public int OnStateChanged([In] AudioSessionState state)
		{
			this.audioSessionEventsHandler.OnStateChanged(state);
			return 0;
		}

		public int OnSessionDisconnected([In] AudioSessionDisconnectReason disconnectReason)
		{
			this.audioSessionEventsHandler.OnSessionDisconnected(disconnectReason);
			return 0;
		}
	}
}
