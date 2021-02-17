using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public class MidiIn : IDisposable
	{
		private IntPtr hMidiIn = IntPtr.Zero;

		private bool disposed;

		private MidiInterop.MidiInCallback callback;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler<MidiInMessageEventArgs> MessageReceived;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler<MidiInMessageEventArgs> ErrorReceived;

		public static int NumberOfDevices
		{
			get
			{
				return MidiInterop.midiInGetNumDevs();
			}
		}

		public MidiIn(int deviceNo)
		{
			this.callback = new MidiInterop.MidiInCallback(this.Callback);
			MmException.Try(MidiInterop.midiInOpen(out this.hMidiIn, (IntPtr)deviceNo, this.callback, IntPtr.Zero, 196608), "midiInOpen");
		}

		public void Close()
		{
			this.Dispose();
		}

		public void Dispose()
		{
			GC.KeepAlive(this.callback);
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void Start()
		{
			MmException.Try(MidiInterop.midiInStart(this.hMidiIn), "midiInStart");
		}

		public void Stop()
		{
			MmException.Try(MidiInterop.midiInStop(this.hMidiIn), "midiInStop");
		}

		public void Reset()
		{
			MmException.Try(MidiInterop.midiInReset(this.hMidiIn), "midiInReset");
		}

		private void Callback(IntPtr midiInHandle, MidiInterop.MidiInMessage message, IntPtr userData, IntPtr messageParameter1, IntPtr messageParameter2)
		{
			switch (message)
			{
			case MidiInterop.MidiInMessage.Open:
			case MidiInterop.MidiInMessage.Close:
			case MidiInterop.MidiInMessage.LongData:
			case MidiInterop.MidiInMessage.LongError:
			case (MidiInterop.MidiInMessage)967:
			case (MidiInterop.MidiInMessage)968:
			case (MidiInterop.MidiInMessage)969:
			case (MidiInterop.MidiInMessage)970:
			case (MidiInterop.MidiInMessage)971:
			case MidiInterop.MidiInMessage.MoreData:
				break;
			case MidiInterop.MidiInMessage.Data:
				if (this.MessageReceived != null)
				{
					this.MessageReceived(this, new MidiInMessageEventArgs(messageParameter1.ToInt32(), messageParameter2.ToInt32()));
					return;
				}
				break;
			case MidiInterop.MidiInMessage.Error:
				if (this.ErrorReceived != null)
				{
					this.ErrorReceived(this, new MidiInMessageEventArgs(messageParameter1.ToInt32(), messageParameter2.ToInt32()));
				}
				break;
			default:
				return;
			}
		}

		public static MidiInCapabilities DeviceInfo(int midiInDeviceNumber)
		{
			MidiInCapabilities midiInCapabilities = default(MidiInCapabilities);
			int size = Marshal.SizeOf(midiInCapabilities);
			MmException.Try(MidiInterop.midiInGetDevCaps((IntPtr)midiInDeviceNumber, out midiInCapabilities, size), "midiInGetDevCaps");
			return midiInCapabilities;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				MidiInterop.midiInClose(this.hMidiIn);
			}
			this.disposed = true;
		}

		~MidiIn()
		{
			this.Dispose(false);
		}
	}
}
