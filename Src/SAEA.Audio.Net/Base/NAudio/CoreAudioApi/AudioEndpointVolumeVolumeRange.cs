using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioEndpointVolumeVolumeRange
    {
        private readonly float volumeMinDecibels;

        private readonly float volumeMaxDecibels;

        private readonly float volumeIncrementDecibels;

        public float MinDecibels
        {
            get
            {
                return this.volumeMinDecibels;
            }
        }

        public float MaxDecibels
        {
            get
            {
                return this.volumeMaxDecibels;
            }
        }

        public float IncrementDecibels
        {
            get
            {
                return this.volumeIncrementDecibels;
            }
        }

        internal AudioEndpointVolumeVolumeRange(IAudioEndpointVolume parent)
        {
            Marshal.ThrowExceptionForHR(parent.GetVolumeRange(out this.volumeMinDecibels, out this.volumeMaxDecibels, out this.volumeIncrementDecibels));
        }
    }
}
