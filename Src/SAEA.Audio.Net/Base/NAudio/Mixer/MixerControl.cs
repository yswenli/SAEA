using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public abstract class MixerControl
	{
		internal MixerInterop.MIXERCONTROL mixerControl;

		internal MixerInterop.MIXERCONTROLDETAILS mixerControlDetails;

		protected IntPtr mixerHandle;

		protected int nChannels;

		protected MixerFlags mixerHandleType;

		public string Name
		{
			get
			{
				return this.mixerControl.szName;
			}
		}

		public MixerControlType ControlType
		{
			get
			{
				return this.mixerControl.dwControlType;
			}
		}

		public bool IsBoolean
		{
			get
			{
				return MixerControl.IsControlBoolean(this.mixerControl.dwControlType);
			}
		}

		public bool IsListText
		{
			get
			{
				return MixerControl.IsControlListText(this.mixerControl.dwControlType);
			}
		}

		public bool IsSigned
		{
			get
			{
				return MixerControl.IsControlSigned(this.mixerControl.dwControlType);
			}
		}

		public bool IsUnsigned
		{
			get
			{
				return MixerControl.IsControlUnsigned(this.mixerControl.dwControlType);
			}
		}

		public bool IsCustom
		{
			get
			{
				return MixerControl.IsControlCustom(this.mixerControl.dwControlType);
			}
		}

		public static IList<MixerControl> GetMixerControls(IntPtr mixerHandle, MixerLine mixerLine, MixerFlags mixerHandleType)
		{
			List<MixerControl> list = new List<MixerControl>();
			if (mixerLine.ControlsCount > 0)
			{
				int num = Marshal.SizeOf(typeof(MixerInterop.MIXERCONTROL));
				MixerInterop.MIXERLINECONTROLS mIXERLINECONTROLS = default(MixerInterop.MIXERLINECONTROLS);
				IntPtr intPtr = Marshal.AllocHGlobal(num * mixerLine.ControlsCount);
				mIXERLINECONTROLS.cbStruct = Marshal.SizeOf(mIXERLINECONTROLS);
				mIXERLINECONTROLS.dwLineID = mixerLine.LineId;
				mIXERLINECONTROLS.cControls = mixerLine.ControlsCount;
				mIXERLINECONTROLS.pamxctrl = intPtr;
				mIXERLINECONTROLS.cbmxctrl = Marshal.SizeOf(typeof(MixerInterop.MIXERCONTROL));
				try
				{
					MmResult mmResult = MixerInterop.mixerGetLineControls(mixerHandle, ref mIXERLINECONTROLS, MixerFlags.Mixer | mixerHandleType);
					if (mmResult != MmResult.NoError)
					{
						throw new MmException(mmResult, "mixerGetLineControls");
					}
					for (int i = 0; i < mIXERLINECONTROLS.cControls; i++)
					{
						MixerInterop.MIXERCONTROL mIXERCONTROL = (MixerInterop.MIXERCONTROL)Marshal.PtrToStructure((IntPtr)(intPtr.ToInt64() + (long)(num * i)), typeof(MixerInterop.MIXERCONTROL));
						MixerControl item = MixerControl.GetMixerControl(mixerHandle, mixerLine.LineId, mIXERCONTROL.dwControlID, mixerLine.Channels, mixerHandleType);
						list.Add(item);
					}
				}
				finally
				{
					Marshal.FreeHGlobal(intPtr);
				}
			}
			return list;
		}

		public static MixerControl GetMixerControl(IntPtr mixerHandle, int nLineID, int controlId, int nChannels, MixerFlags mixerFlags)
		{
			MixerInterop.MIXERLINECONTROLS mIXERLINECONTROLS = default(MixerInterop.MIXERLINECONTROLS);
			MixerInterop.MIXERCONTROL mIXERCONTROL = default(MixerInterop.MIXERCONTROL);
			IntPtr intPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(mIXERCONTROL));
			mIXERLINECONTROLS.cbStruct = Marshal.SizeOf(mIXERLINECONTROLS);
			mIXERLINECONTROLS.cControls = 1;
			mIXERLINECONTROLS.dwControlID = controlId;
			mIXERLINECONTROLS.cbmxctrl = Marshal.SizeOf(mIXERCONTROL);
			mIXERLINECONTROLS.pamxctrl = intPtr;
			mIXERLINECONTROLS.dwLineID = nLineID;
			MmResult mmResult = MixerInterop.mixerGetLineControls(mixerHandle, ref mIXERLINECONTROLS, MixerFlags.ListText | mixerFlags);
			if (mmResult != MmResult.NoError)
			{
				Marshal.FreeCoTaskMem(intPtr);
				throw new MmException(mmResult, "mixerGetLineControls");
			}
			mIXERCONTROL = (MixerInterop.MIXERCONTROL)Marshal.PtrToStructure(mIXERLINECONTROLS.pamxctrl, typeof(MixerInterop.MIXERCONTROL));
			Marshal.FreeCoTaskMem(intPtr);
			if (MixerControl.IsControlBoolean(mIXERCONTROL.dwControlType))
			{
				return new BooleanMixerControl(mIXERCONTROL, mixerHandle, mixerFlags, nChannels);
			}
			if (MixerControl.IsControlSigned(mIXERCONTROL.dwControlType))
			{
				return new SignedMixerControl(mIXERCONTROL, mixerHandle, mixerFlags, nChannels);
			}
			if (MixerControl.IsControlUnsigned(mIXERCONTROL.dwControlType))
			{
				return new UnsignedMixerControl(mIXERCONTROL, mixerHandle, mixerFlags, nChannels);
			}
			if (MixerControl.IsControlListText(mIXERCONTROL.dwControlType))
			{
				return new ListTextMixerControl(mIXERCONTROL, mixerHandle, mixerFlags, nChannels);
			}
			if (MixerControl.IsControlCustom(mIXERCONTROL.dwControlType))
			{
				return new CustomMixerControl(mIXERCONTROL, mixerHandle, mixerFlags, nChannels);
			}
			throw new InvalidOperationException(string.Format("Unknown mixer control type {0}", mIXERCONTROL.dwControlType));
		}

		protected void GetControlDetails()
		{
			this.mixerControlDetails.cbStruct = Marshal.SizeOf(this.mixerControlDetails);
			this.mixerControlDetails.dwControlID = this.mixerControl.dwControlID;
			if (this.IsCustom)
			{
				this.mixerControlDetails.cChannels = 0;
			}
			else if ((this.mixerControl.fdwControl & 1u) != 0u)
			{
				this.mixerControlDetails.cChannels = 1;
			}
			else
			{
				this.mixerControlDetails.cChannels = this.nChannels;
			}
			if ((this.mixerControl.fdwControl & 2u) != 0u)
			{
				this.mixerControlDetails.hwndOwner = (IntPtr)((long)((ulong)this.mixerControl.cMultipleItems));
			}
			else if (this.IsCustom)
			{
				this.mixerControlDetails.hwndOwner = IntPtr.Zero;
			}
			else
			{
				this.mixerControlDetails.hwndOwner = IntPtr.Zero;
			}
			if (this.IsBoolean)
			{
				this.mixerControlDetails.cbDetails = Marshal.SizeOf(default(MixerInterop.MIXERCONTROLDETAILS_BOOLEAN));
			}
			else if (this.IsListText)
			{
				this.mixerControlDetails.cbDetails = Marshal.SizeOf(default(MixerInterop.MIXERCONTROLDETAILS_LISTTEXT));
			}
			else if (this.IsSigned)
			{
				this.mixerControlDetails.cbDetails = Marshal.SizeOf(default(MixerInterop.MIXERCONTROLDETAILS_SIGNED));
			}
			else if (this.IsUnsigned)
			{
				this.mixerControlDetails.cbDetails = Marshal.SizeOf(default(MixerInterop.MIXERCONTROLDETAILS_UNSIGNED));
			}
			else
			{
				this.mixerControlDetails.cbDetails = this.mixerControl.Metrics.customData;
			}
			int num = this.mixerControlDetails.cbDetails * this.mixerControlDetails.cChannels;
			if ((this.mixerControl.fdwControl & 2u) != 0u)
			{
				num *= (int)this.mixerControl.cMultipleItems;
			}
			IntPtr intPtr = Marshal.AllocCoTaskMem(num);
			this.mixerControlDetails.paDetails = intPtr;
			MmResult mmResult = MixerInterop.mixerGetControlDetails(this.mixerHandle, ref this.mixerControlDetails, MixerFlags.Mixer | this.mixerHandleType);
			if (mmResult == MmResult.NoError)
			{
				this.GetDetails(this.mixerControlDetails.paDetails);
			}
			Marshal.FreeCoTaskMem(intPtr);
			if (mmResult != MmResult.NoError)
			{
				throw new MmException(mmResult, "mixerGetControlDetails");
			}
		}

		protected abstract void GetDetails(IntPtr pDetails);

		private static bool IsControlBoolean(MixerControlType controlType)
		{
			if (controlType <= MixerControlType.Button)
			{
				if (controlType != MixerControlType.BooleanMeter)
				{
					switch (controlType)
					{
					case MixerControlType.Boolean:
					case MixerControlType.OnOff:
					case MixerControlType.Mute:
					case MixerControlType.Mono:
					case MixerControlType.Loudness:
					case MixerControlType.StereoEnhance:
						break;
					default:
						if (controlType != MixerControlType.Button)
						{
							return false;
						}
						break;
					}
				}
			}
			else if (controlType <= MixerControlType.Mux)
			{
				if (controlType != MixerControlType.SingleSelect && controlType != MixerControlType.Mux)
				{
					return false;
				}
			}
			else if (controlType != MixerControlType.MultipleSelect && controlType != MixerControlType.Mixer)
			{
				return false;
			}
			return true;
		}

		private static bool IsControlListText(MixerControlType controlType)
		{
			if (controlType <= MixerControlType.SingleSelect)
			{
				if (controlType != MixerControlType.Equalizer && controlType != MixerControlType.SingleSelect)
				{
					return false;
				}
			}
			else if (controlType != MixerControlType.Mux && controlType != MixerControlType.MultipleSelect && controlType != MixerControlType.Mixer)
			{
				return false;
			}
			return true;
		}

		private static bool IsControlSigned(MixerControlType controlType)
		{
			if (controlType <= MixerControlType.PeakMeter)
			{
				if (controlType != MixerControlType.SignedMeter && controlType != MixerControlType.PeakMeter)
				{
					return false;
				}
			}
			else if (controlType != MixerControlType.Signed && controlType != MixerControlType.Decibels)
			{
				switch (controlType)
				{
				case MixerControlType.Slider:
				case MixerControlType.Pan:
				case MixerControlType.QSoundPan:
					break;
				default:
					return false;
				}
			}
			return true;
		}

		private static bool IsControlUnsigned(MixerControlType controlType)
		{
			if (controlType <= MixerControlType.Percent)
			{
				if (controlType != MixerControlType.UnsignedMeter && controlType != MixerControlType.Unsigned && controlType != MixerControlType.Percent)
				{
					return false;
				}
			}
			else
			{
				switch (controlType)
				{
				case MixerControlType.Fader:
				case MixerControlType.Volume:
				case MixerControlType.Bass:
				case MixerControlType.Treble:
				case MixerControlType.Equalizer:
					break;
				default:
					if (controlType != MixerControlType.MicroTime && controlType != MixerControlType.MilliTime)
					{
						return false;
					}
					break;
				}
			}
			return true;
		}

		private static bool IsControlCustom(MixerControlType controlType)
		{
			return controlType == MixerControlType.Custom;
		}

		public override string ToString()
		{
			return string.Format("{0} {1}", this.Name, this.ControlType);
		}
	}
}
