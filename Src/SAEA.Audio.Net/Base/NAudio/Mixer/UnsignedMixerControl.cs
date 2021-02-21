using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public class UnsignedMixerControl : MixerControl
	{
		private MixerInterop.MIXERCONTROLDETAILS_UNSIGNED[] unsignedDetails;

		public uint Value
		{
			get
			{
				base.GetControlDetails();
				return this.unsignedDetails[0].dwValue;
			}
			set
			{
				int num = Marshal.SizeOf(this.unsignedDetails[0]);
				this.mixerControlDetails.paDetails = Marshal.AllocHGlobal(num * this.nChannels);
				for (int i = 0; i < this.nChannels; i++)
				{
					this.unsignedDetails[i].dwValue = value;
					long value2 = this.mixerControlDetails.paDetails.ToInt64() + (long)(num * i);
					Marshal.StructureToPtr(this.unsignedDetails[i], (IntPtr)value2, false);
				}
				MmException.Try(MixerInterop.mixerSetControlDetails(this.mixerHandle, ref this.mixerControlDetails, MixerFlags.Mixer | this.mixerHandleType), "mixerSetControlDetails");
				Marshal.FreeHGlobal(this.mixerControlDetails.paDetails);
			}
		}

		public uint MinValue
		{
			get
			{
				return (uint)this.mixerControl.Bounds.minimum;
			}
		}

		public uint MaxValue
		{
			get
			{
				return (uint)this.mixerControl.Bounds.maximum;
			}
		}

		public double Percent
		{
			get
			{
				return 100.0 * (this.Value - this.MinValue) / (this.MaxValue - this.MinValue);
			}
			set
			{
				this.Value = (uint)(this.MinValue + value / 100.0 * (this.MaxValue - this.MinValue));
			}
		}

		internal UnsignedMixerControl(MixerInterop.MIXERCONTROL mixerControl, IntPtr mixerHandle, MixerFlags mixerHandleType, int nChannels)
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
			this.unsignedDetails = new MixerInterop.MIXERCONTROLDETAILS_UNSIGNED[this.nChannels];
			for (int i = 0; i < this.nChannels; i++)
			{
				this.unsignedDetails[i] = (MixerInterop.MIXERCONTROLDETAILS_UNSIGNED)Marshal.PtrToStructure(this.mixerControlDetails.paDetails, typeof(MixerInterop.MIXERCONTROLDETAILS_UNSIGNED));
			}
		}

		public override string ToString()
		{
			return string.Format("{0} {1}%", base.ToString(), this.Percent);
		}
	}
}
