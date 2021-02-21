using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class AudioEndpointVolumeStepInformation
    {
        private readonly uint step;

        private readonly uint stepCount;

        public uint Step
        {
            get
            {
                return this.step;
            }
        }

        public uint StepCount
        {
            get
            {
                return this.stepCount;
            }
        }

        internal AudioEndpointVolumeStepInformation(IAudioEndpointVolume parent)
        {
            Marshal.ThrowExceptionForHR(parent.GetVolumeStepInfo(out this.step, out this.stepCount));
        }
    }
}
