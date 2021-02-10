using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Mixer
{
	public class MixerLine
	{
		private MixerInterop.MIXERLINE mixerLine;

		private IntPtr mixerHandle;

		private MixerFlags mixerHandleType;

		public string Name
		{
			get
			{
				return this.mixerLine.szName;
			}
		}

		public string ShortName
		{
			get
			{
				return this.mixerLine.szShortName;
			}
		}

		public int LineId
		{
			get
			{
				return this.mixerLine.dwLineID;
			}
		}

		public MixerLineComponentType ComponentType
		{
			get
			{
				return this.mixerLine.dwComponentType;
			}
		}

		public string TypeDescription
		{
			get
			{
				MixerLineComponentType dwComponentType = this.mixerLine.dwComponentType;
				switch (dwComponentType)
				{
				case MixerLineComponentType.DestinationUndefined:
					return "Undefined Destination";
				case MixerLineComponentType.DestinationDigital:
					return "Digital Destination";
				case MixerLineComponentType.DestinationLine:
					return "Line Level Destination";
				case MixerLineComponentType.DestinationMonitor:
					return "Monitor Destination";
				case MixerLineComponentType.DestinationSpeakers:
					return "Speakers Destination";
				case MixerLineComponentType.DestinationHeadphones:
					return "Headphones Destination";
				case MixerLineComponentType.DestinationTelephone:
					return "Telephone Destination";
				case MixerLineComponentType.DestinationWaveIn:
					return "Wave Input Destination";
				case MixerLineComponentType.DestinationVoiceIn:
					return "Voice Recognition Destination";
				default:
					switch (dwComponentType)
					{
					case MixerLineComponentType.SourceUndefined:
						return "Undefined Source";
					case MixerLineComponentType.SourceDigital:
						return "Digital Source";
					case MixerLineComponentType.SourceLine:
						return "Line Level Source";
					case MixerLineComponentType.SourceMicrophone:
						return "Microphone Source";
					case MixerLineComponentType.SourceSynthesizer:
						return "Synthesizer Source";
					case MixerLineComponentType.SourceCompactDisc:
						return "Compact Disk Source";
					case MixerLineComponentType.SourceTelephone:
						return "Telephone Source";
					case MixerLineComponentType.SourcePcSpeaker:
						return "PC Speaker Source";
					case MixerLineComponentType.SourceWaveOut:
						return "Wave Out Source";
					case MixerLineComponentType.SourceAuxiliary:
						return "Auxiliary Source";
					case MixerLineComponentType.SourceAnalog:
						return "Analog Source";
					default:
						return "Invalid Component Type";
					}
					break;
				}
			}
		}

		public int Channels
		{
			get
			{
				return this.mixerLine.cChannels;
			}
		}

		public int SourceCount
		{
			get
			{
				return this.mixerLine.cConnections;
			}
		}

		public int ControlsCount
		{
			get
			{
				return this.mixerLine.cControls;
			}
		}

		public bool IsActive
		{
			get
			{
				return (this.mixerLine.fdwLine & MixerInterop.MIXERLINE_LINEF.MIXERLINE_LINEF_ACTIVE) > (MixerInterop.MIXERLINE_LINEF)0;
			}
		}

		public bool IsDisconnected
		{
			get
			{
				return (this.mixerLine.fdwLine & MixerInterop.MIXERLINE_LINEF.MIXERLINE_LINEF_DISCONNECTED) > (MixerInterop.MIXERLINE_LINEF)0;
			}
		}

		public bool IsSource
		{
			get
			{
				return (this.mixerLine.fdwLine & MixerInterop.MIXERLINE_LINEF.MIXERLINE_LINEF_SOURCE) > (MixerInterop.MIXERLINE_LINEF)0;
			}
		}

		public IEnumerable<MixerControl> Controls
		{
			get
			{
				return MixerControl.GetMixerControls(this.mixerHandle, this, this.mixerHandleType);
			}
		}

		public IEnumerable<MixerLine> Sources
		{
			get
			{
				int num;
				for (int i = 0; i < this.SourceCount; i = num + 1)
				{
					yield return this.GetSource(i);
					num = i;
				}
				yield break;
			}
		}

		public string TargetName
		{
			get
			{
				return this.mixerLine.szPname;
			}
		}

		public MixerLine(IntPtr mixerHandle, int destinationIndex, MixerFlags mixerHandleType)
		{
			this.mixerHandle = mixerHandle;
			this.mixerHandleType = mixerHandleType;
			this.mixerLine = default(MixerInterop.MIXERLINE);
			this.mixerLine.cbStruct = Marshal.SizeOf(this.mixerLine);
			this.mixerLine.dwDestination = destinationIndex;
			MmException.Try(MixerInterop.mixerGetLineInfo(mixerHandle, ref this.mixerLine, mixerHandleType | MixerFlags.Mixer), "mixerGetLineInfo");
		}

		public MixerLine(IntPtr mixerHandle, int destinationIndex, int sourceIndex, MixerFlags mixerHandleType)
		{
			this.mixerHandle = mixerHandle;
			this.mixerHandleType = mixerHandleType;
			this.mixerLine = default(MixerInterop.MIXERLINE);
			this.mixerLine.cbStruct = Marshal.SizeOf(this.mixerLine);
			this.mixerLine.dwDestination = destinationIndex;
			this.mixerLine.dwSource = sourceIndex;
			MmException.Try(MixerInterop.mixerGetLineInfo(mixerHandle, ref this.mixerLine, mixerHandleType | MixerFlags.ListText), "mixerGetLineInfo");
		}

		public static int GetMixerIdForWaveIn(int waveInDevice)
		{
			int result = -1;
			MmException.Try(MixerInterop.mixerGetID((IntPtr)waveInDevice, out result, MixerFlags.WaveIn), "mixerGetID");
			return result;
		}

		public MixerLine GetSource(int sourceIndex)
		{
			if (sourceIndex < 0 || sourceIndex >= this.SourceCount)
			{
				throw new ArgumentOutOfRangeException("sourceIndex");
			}
			return new MixerLine(this.mixerHandle, this.mixerLine.dwDestination, sourceIndex, this.mixerHandleType);
		}

		public override string ToString()
		{
			return string.Format("{0} {1} ({2} controls, ID={3})", new object[]
			{
				this.Name,
				this.TypeDescription,
				this.ControlsCount,
				this.mixerLine.dwLineID
			});
		}
	}
}
