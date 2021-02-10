using Microsoft.Win32;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SAEA.Audio.NAudio.Wave.Asio
{
	public class AsioDriver
	{
		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private class AsioDriverVTable
		{
			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate int ASIOInit(IntPtr _pUnknown, IntPtr sysHandle);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate void ASIOgetDriverName(IntPtr _pUnknown, StringBuilder name);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate int ASIOgetDriverVersion(IntPtr _pUnknown);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate void ASIOgetErrorMessage(IntPtr _pUnknown, StringBuilder errorMessage);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOstart(IntPtr _pUnknown);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOstop(IntPtr _pUnknown);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetChannels(IntPtr _pUnknown, out int numInputChannels, out int numOutputChannels);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetLatencies(IntPtr _pUnknown, out int inputLatency, out int outputLatency);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetBufferSize(IntPtr _pUnknown, out int minSize, out int maxSize, out int preferredSize, out int granularity);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOcanSampleRate(IntPtr _pUnknown, double sampleRate);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetSampleRate(IntPtr _pUnknown, out double sampleRate);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOsetSampleRate(IntPtr _pUnknown, double sampleRate);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetClockSources(IntPtr _pUnknown, out long clocks, int numSources);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOsetClockSource(IntPtr _pUnknown, int reference);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetSamplePosition(IntPtr _pUnknown, out long samplePos, ref Asio64Bit timeStamp);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOgetChannelInfo(IntPtr _pUnknown, ref AsioChannelInfo info);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOcreateBuffers(IntPtr _pUnknown, IntPtr bufferInfos, int numChannels, int bufferSize, IntPtr callbacks);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOdisposeBuffers(IntPtr _pUnknown);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOcontrolPanel(IntPtr _pUnknown);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOfuture(IntPtr _pUnknown, int selector, IntPtr opt);

			[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
			public delegate AsioError ASIOoutputReady(IntPtr _pUnknown);

			public AsioDriver.AsioDriverVTable.ASIOInit init;

			public AsioDriver.AsioDriverVTable.ASIOgetDriverName getDriverName;

			public AsioDriver.AsioDriverVTable.ASIOgetDriverVersion getDriverVersion;

			public AsioDriver.AsioDriverVTable.ASIOgetErrorMessage getErrorMessage;

			public AsioDriver.AsioDriverVTable.ASIOstart start;

			public AsioDriver.AsioDriverVTable.ASIOstop stop;

			public AsioDriver.AsioDriverVTable.ASIOgetChannels getChannels;

			public AsioDriver.AsioDriverVTable.ASIOgetLatencies getLatencies;

			public AsioDriver.AsioDriverVTable.ASIOgetBufferSize getBufferSize;

			public AsioDriver.AsioDriverVTable.ASIOcanSampleRate canSampleRate;

			public AsioDriver.AsioDriverVTable.ASIOgetSampleRate getSampleRate;

			public AsioDriver.AsioDriverVTable.ASIOsetSampleRate setSampleRate;

			public AsioDriver.AsioDriverVTable.ASIOgetClockSources getClockSources;

			public AsioDriver.AsioDriverVTable.ASIOsetClockSource setClockSource;

			public AsioDriver.AsioDriverVTable.ASIOgetSamplePosition getSamplePosition;

			public AsioDriver.AsioDriverVTable.ASIOgetChannelInfo getChannelInfo;

			public AsioDriver.AsioDriverVTable.ASIOcreateBuffers createBuffers;

			public AsioDriver.AsioDriverVTable.ASIOdisposeBuffers disposeBuffers;

			public AsioDriver.AsioDriverVTable.ASIOcontrolPanel controlPanel;

			public AsioDriver.AsioDriverVTable.ASIOfuture future;

			public AsioDriver.AsioDriverVTable.ASIOoutputReady outputReady;
		}

		private IntPtr pAsioComObject;

		private IntPtr pinnedcallbacks;

		private AsioDriver.AsioDriverVTable asioDriverVTable;

		private AsioDriver()
		{
		}

		public static string[] GetAsioDriverNames()
		{
			RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\ASIO");
			string[] result = new string[0];
			if (registryKey != null)
			{
				result = registryKey.GetSubKeyNames();
				registryKey.Close();
			}
			return result;
		}

		public static AsioDriver GetAsioDriverByName(string name)
		{
			RegistryKey expr_15 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\ASIO\\" + name);
			if (expr_15 == null)
			{
				throw new ArgumentException(string.Format("Driver Name {0} doesn't exist", name));
			}
			return AsioDriver.GetAsioDriverByGuid(new Guid(expr_15.GetValue("CLSID").ToString()));
		}

		public static AsioDriver GetAsioDriverByGuid(Guid guid)
		{
			AsioDriver expr_05 = new AsioDriver();
			expr_05.InitFromGuid(guid);
			return expr_05;
		}

		public bool Init(IntPtr sysHandle)
		{
			return this.asioDriverVTable.init(this.pAsioComObject, sysHandle) == 1;
		}

		public string GetDriverName()
		{
			StringBuilder stringBuilder = new StringBuilder(256);
			this.asioDriverVTable.getDriverName(this.pAsioComObject, stringBuilder);
			return stringBuilder.ToString();
		}

		public int GetDriverVersion()
		{
			return this.asioDriverVTable.getDriverVersion(this.pAsioComObject);
		}

		public string GetErrorMessage()
		{
			StringBuilder stringBuilder = new StringBuilder(256);
			this.asioDriverVTable.getErrorMessage(this.pAsioComObject, stringBuilder);
			return stringBuilder.ToString();
		}

		public void Start()
		{
			this.HandleException(this.asioDriverVTable.start(this.pAsioComObject), "start");
		}

		public AsioError Stop()
		{
			return this.asioDriverVTable.stop(this.pAsioComObject);
		}

		public void GetChannels(out int numInputChannels, out int numOutputChannels)
		{
			this.HandleException(this.asioDriverVTable.getChannels(this.pAsioComObject, out numInputChannels, out numOutputChannels), "getChannels");
		}

		public AsioError GetLatencies(out int inputLatency, out int outputLatency)
		{
			return this.asioDriverVTable.getLatencies(this.pAsioComObject, out inputLatency, out outputLatency);
		}

		public void GetBufferSize(out int minSize, out int maxSize, out int preferredSize, out int granularity)
		{
			this.HandleException(this.asioDriverVTable.getBufferSize(this.pAsioComObject, out minSize, out maxSize, out preferredSize, out granularity), "getBufferSize");
		}

		public bool CanSampleRate(double sampleRate)
		{
			AsioError asioError = this.asioDriverVTable.canSampleRate(this.pAsioComObject, sampleRate);
			if (asioError == AsioError.ASE_NoClock)
			{
				return false;
			}
			if (asioError == AsioError.ASE_OK)
			{
				return true;
			}
			this.HandleException(asioError, "canSampleRate");
			return false;
		}

		public double GetSampleRate()
		{
			double result;
			this.HandleException(this.asioDriverVTable.getSampleRate(this.pAsioComObject, out result), "getSampleRate");
			return result;
		}

		public void SetSampleRate(double sampleRate)
		{
			this.HandleException(this.asioDriverVTable.setSampleRate(this.pAsioComObject, sampleRate), "setSampleRate");
		}

		public void GetClockSources(out long clocks, int numSources)
		{
			this.HandleException(this.asioDriverVTable.getClockSources(this.pAsioComObject, out clocks, numSources), "getClockSources");
		}

		public void SetClockSource(int reference)
		{
			this.HandleException(this.asioDriverVTable.setClockSource(this.pAsioComObject, reference), "setClockSources");
		}

		public void GetSamplePosition(out long samplePos, ref Asio64Bit timeStamp)
		{
			this.HandleException(this.asioDriverVTable.getSamplePosition(this.pAsioComObject, out samplePos, ref timeStamp), "getSamplePosition");
		}

		public AsioChannelInfo GetChannelInfo(int channelNumber, bool trueForInputInfo)
		{
			AsioChannelInfo result = new AsioChannelInfo
			{
				channel = channelNumber,
				isInput = trueForInputInfo
			};
			this.HandleException(this.asioDriverVTable.getChannelInfo(this.pAsioComObject, ref result), "getChannelInfo");
			return result;
		}

		public void CreateBuffers(IntPtr bufferInfos, int numChannels, int bufferSize, ref AsioCallbacks callbacks)
		{
			this.pinnedcallbacks = Marshal.AllocHGlobal(Marshal.SizeOf(callbacks));
			Marshal.StructureToPtr(callbacks, this.pinnedcallbacks, false);
			this.HandleException(this.asioDriverVTable.createBuffers(this.pAsioComObject, bufferInfos, numChannels, bufferSize, this.pinnedcallbacks), "createBuffers");
		}

		public AsioError DisposeBuffers()
		{
			AsioError arg_21_0 = this.asioDriverVTable.disposeBuffers(this.pAsioComObject);
			Marshal.FreeHGlobal(this.pinnedcallbacks);
			return arg_21_0;
		}

		public void ControlPanel()
		{
			this.HandleException(this.asioDriverVTable.controlPanel(this.pAsioComObject), "controlPanel");
		}

		public void Future(int selector, IntPtr opt)
		{
			this.HandleException(this.asioDriverVTable.future(this.pAsioComObject, selector, opt), "future");
		}

		public AsioError OutputReady()
		{
			return this.asioDriverVTable.outputReady(this.pAsioComObject);
		}

		public void ReleaseComAsioDriver()
		{
			Marshal.Release(this.pAsioComObject);
		}

		private void HandleException(AsioError error, string methodName)
		{
			if (error != AsioError.ASE_OK && error != AsioError.ASE_SUCCESS)
			{
				throw new AsioException(string.Format("Error code [{0}] while calling ASIO method <{1}>, {2}", AsioException.getErrorName(error), methodName, this.GetErrorMessage()))
				{
					Error = error
				};
			}
		}

		private void InitFromGuid(Guid asioGuid)
		{
			int num = AsioDriver.CoCreateInstance(ref asioGuid, IntPtr.Zero, 1u, ref asioGuid, out this.pAsioComObject);
			if (num != 0)
			{
				throw new COMException("Unable to instantiate ASIO. Check if STAThread is set", num);
			}
			IntPtr ptr = Marshal.ReadIntPtr(this.pAsioComObject);
			this.asioDriverVTable = new AsioDriver.AsioDriverVTable();
			FieldInfo[] fields = typeof(AsioDriver.AsioDriverVTable).GetFields();
			for (int i = 0; i < fields.Length; i++)
			{
				FieldInfo fieldInfo = fields[i];
				object delegateForFunctionPointer = Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(ptr, (i + 3) * IntPtr.Size), fieldInfo.FieldType);
				fieldInfo.SetValue(this.asioDriverVTable, delegateForFunctionPointer);
			}
		}

		[DllImport("ole32.Dll")]
		private static extern int CoCreateInstance(ref Guid clsid, IntPtr inner, uint context, ref Guid uuid, out IntPtr rReturnedComObject);
	}
}
