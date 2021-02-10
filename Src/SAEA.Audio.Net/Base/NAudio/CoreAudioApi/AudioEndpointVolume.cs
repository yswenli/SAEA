using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioEndpointVolume : IDisposable
    {
        private readonly IAudioEndpointVolume audioEndPointVolume;

        private readonly AudioEndpointVolumeChannels channels;

        private readonly AudioEndpointVolumeStepInformation stepInformation;

        private readonly AudioEndpointVolumeVolumeRange volumeRange;

        private readonly EEndpointHardwareSupport hardwareSupport;

        private AudioEndpointVolumeCallback callBack;

        private Guid notificationGuid = Guid.Empty;

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event AudioEndpointVolumeNotificationDelegate OnVolumeNotification;

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

        public AudioEndpointVolumeVolumeRange VolumeRange
        {
            get
            {
                return this.volumeRange;
            }
        }

        public EEndpointHardwareSupport HardwareSupport
        {
            get
            {
                return this.hardwareSupport;
            }
        }

        public AudioEndpointVolumeStepInformation StepInformation
        {
            get
            {
                return this.stepInformation;
            }
        }

        public AudioEndpointVolumeChannels Channels
        {
            get
            {
                return this.channels;
            }
        }

        public float MasterVolumeLevel
        {
            get
            {
                float result;
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.GetMasterVolumeLevel(out result));
                return result;
            }
            set
            {
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.SetMasterVolumeLevel(value, ref this.notificationGuid));
            }
        }

        public float MasterVolumeLevelScalar
        {
            get
            {
                float result;
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.GetMasterVolumeLevelScalar(out result));
                return result;
            }
            set
            {
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.SetMasterVolumeLevelScalar(value, ref this.notificationGuid));
            }
        }

        public bool Mute
        {
            get
            {
                bool result;
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.GetMute(out result));
                return result;
            }
            set
            {
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.SetMute(value, ref this.notificationGuid));
            }
        }

        public void VolumeStepUp()
        {
            Marshal.ThrowExceptionForHR(this.audioEndPointVolume.VolumeStepUp(ref this.notificationGuid));
        }

        public void VolumeStepDown()
        {
            Marshal.ThrowExceptionForHR(this.audioEndPointVolume.VolumeStepDown(ref this.notificationGuid));
        }

        internal AudioEndpointVolume(IAudioEndpointVolume realEndpointVolume)
        {
            this.audioEndPointVolume = realEndpointVolume;
            this.channels = new AudioEndpointVolumeChannels(this.audioEndPointVolume);
            this.stepInformation = new AudioEndpointVolumeStepInformation(this.audioEndPointVolume);
            uint num;
            Marshal.ThrowExceptionForHR(this.audioEndPointVolume.QueryHardwareSupport(out num));
            this.hardwareSupport = (EEndpointHardwareSupport)num;
            this.volumeRange = new AudioEndpointVolumeVolumeRange(this.audioEndPointVolume);
            this.callBack = new AudioEndpointVolumeCallback(this);
            Marshal.ThrowExceptionForHR(this.audioEndPointVolume.RegisterControlChangeNotify(this.callBack));
        }

        internal void FireNotification(AudioVolumeNotificationData notificationData)
        {
            AudioEndpointVolumeNotificationDelegate onVolumeNotification = this.OnVolumeNotification;
            if (onVolumeNotification != null)
            {
                onVolumeNotification(notificationData);
            }
        }

        public void Dispose()
        {
            if (this.callBack != null)
            {
                Marshal.ThrowExceptionForHR(this.audioEndPointVolume.UnregisterControlChangeNotify(this.callBack));
                this.callBack = null;
            }
            Marshal.ReleaseComObject(this.audioEndPointVolume);
            GC.SuppressFinalize(this);
        }

        ~AudioEndpointVolume()
        {
            this.Dispose();
        }
    }
}
