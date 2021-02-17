using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public class SignedMixerControl : MixerControl
	{
		private MixerInterop.MIXERCONTROLDETAILS_SIGNED signedDetails;

		public int Value
		{
			get
			{
				base.GetControlDetails();
				return this.signedDetails.lValue;
			}
			set
			{
				this.signedDetails.lValue = value;
				this.mixerControlDetails.paDetails = Marshal.AllocHGlobal(Marshal.SizeOf(this.signedDetails));
				Marshal.StructureToPtr(this.signedDetails, this.mixerControlDetails.paDetails, false);
				MmException.Try(MixerInterop.mixerSetControlDetails(this.mixerHandle, ref this.mixerControlDetails, MixerFlags.Mixer | this.mixerHandleType), "mixerSetControlDetails");
				Marshal.FreeHGlobal(this.mixerControlDetails.paDetails);
			}
		}

		public int MinValue
		{
			get
			{
				return this.mixerControl.Bounds.minimum;
			}
		}

		public int MaxValue
		{
			get
			{
				return this.mixerControl.Bounds.maximum;
			}
		}

		public double Percent
		{
			get
			{
				return 100.0 * (double)(this.Value - this.MinValue) / (double)(this.MaxValue - this.MinValue);
			}
			set
			{
				this.Value = (int)((double)this.MinValue + value / 100.0 * (double)(this.MaxValue - this.MinValue));
			}
		}

		internal SignedMixerControl(MixerInterop.MIXERCONTROL mixerControl, IntPtr mixerHandle, MixerFlags mixerHandleType, int nChannels)
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
			this.signedDetails = (MixerInterop.MIXERCONTROLDETAILS_SIGNED)Marshal.PtrToStructure(this.mixerControlDetails.paDetails, typeof(MixerInterop.MIXERCONTROLDETAILS_SIGNED));
		}

		public override string ToString()
		{
			return string.Format("{0} {1}%", base.ToString(), this.Percent);
		}
	}
}
