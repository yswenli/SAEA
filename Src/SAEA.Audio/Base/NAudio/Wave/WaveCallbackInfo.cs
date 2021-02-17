using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class WaveCallbackInfo
	{

		//private WaveWindow waveOutWindow;

		//private WaveWindowNative waveOutWindowNative;

		public WaveCallbackStrategy Strategy
		{
			get;
			private set;
		}

		public IntPtr Handle
		{
			get;
			private set;
		}

		public static WaveCallbackInfo FunctionCallback()
		{
			return new WaveCallbackInfo(WaveCallbackStrategy.FunctionCallback, IntPtr.Zero);
		}

		public static WaveCallbackInfo NewWindow()
		{
			return new WaveCallbackInfo(WaveCallbackStrategy.NewWindow, IntPtr.Zero);
		}

		public static WaveCallbackInfo ExistingWindow(IntPtr handle)
		{
			if (handle == IntPtr.Zero)
			{
				throw new ArgumentException("Handle cannot be zero");
			}
			return new WaveCallbackInfo(WaveCallbackStrategy.ExistingWindow, handle);
		}

		private WaveCallbackInfo(WaveCallbackStrategy strategy, IntPtr handle)
		{
			this.Strategy = strategy;
			this.Handle = handle;
		}

		internal void Connect(WaveInterop.WaveCallback callback)
		{
			if (this.Strategy == WaveCallbackStrategy.NewWindow)
			{
				//this.waveOutWindow = new WaveWindow(callback);
				//this.waveOutWindow.CreateControl();
				//this.Handle = this.waveOutWindow.Handle;
				return;
			}
			if (this.Strategy == WaveCallbackStrategy.ExistingWindow)
			{
				//this.waveOutWindowNative = new WaveWindowNative(callback);
				//this.waveOutWindowNative.AssignHandle(this.Handle);
			}
		}

		internal MmResult WaveOutOpen(out IntPtr waveOutHandle, int deviceNumber, WaveFormat waveFormat, WaveInterop.WaveCallback callback)
		{
			MmResult result;
			if (this.Strategy == WaveCallbackStrategy.FunctionCallback)
			{
				result = WaveInterop.waveOutOpen(out waveOutHandle, (IntPtr)deviceNumber, waveFormat, callback, IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackFunction);
			}
			else
			{
				result = WaveInterop.waveOutOpenWindow(out waveOutHandle, (IntPtr)deviceNumber, waveFormat, this.Handle, IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackWindow);
			}
			return result;
		}

		internal MmResult WaveInOpen(out IntPtr waveInHandle, int deviceNumber, WaveFormat waveFormat, WaveInterop.WaveCallback callback)
		{
			MmResult result;
			if (this.Strategy == WaveCallbackStrategy.FunctionCallback)
			{
				result = WaveInterop.waveInOpen(out waveInHandle, (IntPtr)deviceNumber, waveFormat, callback, IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackFunction);
			}
			else
			{
				result = WaveInterop.waveInOpenWindow(out waveInHandle, (IntPtr)deviceNumber, waveFormat, this.Handle, IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackWindow);
			}
			return result;
		}

		internal void Disconnect()
		{
			//if (this.waveOutWindow != null)
			//{
			//	this.waveOutWindow.Close();
			//	this.waveOutWindow = null;
			//}
			//if (this.waveOutWindowNative != null)
			//{
			//	this.waveOutWindowNative.ReleaseHandle();
			//	this.waveOutWindowNative = null;
			//}
		}
	}
}
