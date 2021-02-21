using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    internal class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly AudioEndpointVolume parent;

        internal AudioEndpointVolumeCallback(AudioEndpointVolume parent)
        {
            this.parent = parent;
        }

        public void OnNotify(IntPtr notifyData)
        {
            AudioVolumeNotificationDataStruct audioVolumeNotificationDataStruct = MarshalHelpers.PtrToStructure<AudioVolumeNotificationDataStruct>(notifyData);
            IntPtr value = MarshalHelpers.OffsetOf<AudioVolumeNotificationDataStruct>("ChannelVolume");
            IntPtr pointer = (IntPtr)((long)notifyData + (long)value);
            float[] array = new float[audioVolumeNotificationDataStruct.nChannels];
            int num = 0;
            while ((long)num < (long)((ulong)audioVolumeNotificationDataStruct.nChannels))
            {
                array[num] = MarshalHelpers.PtrToStructure<float>(pointer);
                num++;
            }
            AudioVolumeNotificationData notificationData = new AudioVolumeNotificationData(audioVolumeNotificationDataStruct.guidEventContext, audioVolumeNotificationDataStruct.bMuted, audioVolumeNotificationDataStruct.fMasterVolume, array, audioVolumeNotificationDataStruct.guidEventContext);
            this.parent.FireNotification(notificationData);
        }
    }
}
