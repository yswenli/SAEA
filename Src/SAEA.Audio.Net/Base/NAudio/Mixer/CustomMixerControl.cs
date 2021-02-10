using System;

namespace SAEA.Audio.NAudio.Mixer
{
	public class CustomMixerControl : MixerControl
	{
		internal CustomMixerControl(MixerInterop.MIXERCONTROL mixerControl, IntPtr mixerHandle, MixerFlags mixerHandleType, int nChannels)
		{
			this.mixerControl = mixerControl;
			this.mixerHandle = mixerHandle;
			this.mixerHandleType = mixerHandleType;
			this.nChannels = nChannels;
			this.mixerControlDetails = default(MixerInterop.MIXERCONTROLDETAILS);
			base.GetControlDetails();
		}

		protected override void GetDetails(IntPtr pDetails)
		{
		}
	}
}
