using System;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces
{
	public interface IAudioSessionEventsHandler
	{
		void OnVolumeChanged(float volume, bool isMuted);

		void OnDisplayNameChanged(string displayName);

		void OnIconPathChanged(string iconPath);

		void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex);

		void OnGroupingParamChanged(ref Guid groupingId);

		void OnStateChanged(AudioSessionState state);

		void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason);
	}
}
