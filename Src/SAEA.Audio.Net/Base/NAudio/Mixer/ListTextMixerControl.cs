using System;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public class ListTextMixerControl : MixerControl
	{
		internal ListTextMixerControl(MixerInterop.MIXERCONTROL mixerControl, IntPtr mixerHandle, MixerFlags mixerHandleType, int nChannels)
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
