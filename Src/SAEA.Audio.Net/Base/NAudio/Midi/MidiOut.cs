using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Midi
{
	public class MidiOut : IDisposable
	{
		private IntPtr hMidiOut = IntPtr.Zero;

		private bool disposed;

		private MidiInterop.MidiOutCallback callback;

		public static int NumberOfDevices
		{
			get
			{
				return MidiInterop.midiOutGetNumDevs();
			}
		}

		public int Volume
		{
			get
			{
				int result = 0;
				MmException.Try(MidiInterop.midiOutGetVolume(this.hMidiOut, ref result), "midiOutGetVolume");
				return result;
			}
			set
			{
				MmException.Try(MidiInterop.midiOutSetVolume(this.hMidiOut, value), "midiOutSetVolume");
			}
		}

		public static MidiOutCapabilities DeviceInfo(int midiOutDeviceNumber)
		{
			MidiOutCapabilities midiOutCapabilities = default(MidiOutCapabilities);
			int uSize = Marshal.SizeOf(midiOutCapabilities);
			MmException.Try(MidiInterop.midiOutGetDevCaps((IntPtr)midiOutDeviceNumber, out midiOutCapabilities, uSize), "midiOutGetDevCaps");
			return midiOutCapabilities;
		}

		public MidiOut(int deviceNo)
		{
			this.callback = new MidiInterop.MidiOutCallback(this.Callback);
			MmException.Try(MidiInterop.midiOutOpen(out this.hMidiOut, (IntPtr)deviceNo, this.callback, IntPtr.Zero, 196608), "midiOutOpen");
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

		public void Reset()
		{
			MmException.Try(MidiInterop.midiOutReset(this.hMidiOut), "midiOutReset");
		}

		public void SendDriverMessage(int message, int param1, int param2)
		{
			MmException.Try(MidiInterop.midiOutMessage(this.hMidiOut, message, (IntPtr)param1, (IntPtr)param2), "midiOutMessage");
		}

		public void Send(int message)
		{
			MmException.Try(MidiInterop.midiOutShortMsg(this.hMidiOut, message), "midiOutShortMsg");
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				MidiInterop.midiOutClose(this.hMidiOut);
			}
			this.disposed = true;
		}

		private void Callback(IntPtr midiInHandle, MidiInterop.MidiOutMessage message, IntPtr userData, IntPtr messageParameter1, IntPtr messageParameter2)
		{
		}

		public void SendBuffer(byte[] byteBuffer)
		{
			MidiInterop.MIDIHDR mIDIHDR = default(MidiInterop.MIDIHDR);
			mIDIHDR.lpData = Marshal.AllocHGlobal(byteBuffer.Length);
			Marshal.Copy(byteBuffer, 0, mIDIHDR.lpData, byteBuffer.Length);
			mIDIHDR.dwBufferLength = byteBuffer.Length;
			mIDIHDR.dwBytesRecorded = byteBuffer.Length;
			int uSize = Marshal.SizeOf(mIDIHDR);
			MidiInterop.midiOutPrepareHeader(this.hMidiOut, ref mIDIHDR, uSize);
			if (MidiInterop.midiOutLongMsg(this.hMidiOut, ref mIDIHDR, uSize) != MmResult.NoError)
			{
				MidiInterop.midiOutUnprepareHeader(this.hMidiOut, ref mIDIHDR, uSize);
			}
			Marshal.FreeHGlobal(mIDIHDR.lpData);
		}

		~MidiOut()
		{
			this.Dispose(false);
		}
	}
}
