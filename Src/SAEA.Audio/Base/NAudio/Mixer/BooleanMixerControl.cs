using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public class BooleanMixerControl : MixerControl
	{
		private MixerInterop.MIXERCONTROLDETAILS_BOOLEAN boolDetails;

		public bool Value
		{
			get
			{
				base.GetControlDetails();
				return this.boolDetails.fValue == 1;
			}
			set
			{
				this.boolDetails.fValue = (value ? 1 : 0);
				this.mixerControlDetails.paDetails = Marshal.AllocHGlobal(Marshal.SizeOf(this.boolDetails));
				Marshal.StructureToPtr(this.boolDetails, this.mixerControlDetails.paDetails, false);
				MmException.Try(MixerInterop.mixerSetControlDetails(this.mixerHandle, ref this.mixerControlDetails, MixerFlags.Mixer | this.mixerHandleType), "mixerSetControlDetails");
				Marshal.FreeHGlobal(this.mixerControlDetails.paDetails);
			}
		}

		internal BooleanMixerControl(MixerInterop.MIXERCONTROL mixerControl, IntPtr mixerHandle, MixerFlags mixerHandleType, int nChannels)
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
			this.boolDetails = (MixerInterop.MIXERCONTROLDETAILS_BOOLEAN)Marshal.PtrToStructure(pDetails, typeof(MixerInterop.MIXERCONTROLDETAILS_BOOLEAN));
		}
	}
}
