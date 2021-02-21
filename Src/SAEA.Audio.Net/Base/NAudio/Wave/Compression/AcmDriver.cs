using SAEA.Audio.Base.NAudio.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
    public class AcmDriver : IDisposable
	{
		private static List<AcmDriver> drivers;

		private AcmDriverDetails details;

		private IntPtr driverId;

		private IntPtr driverHandle;

		private List<AcmFormatTag> formatTags;

		private List<AcmFormat> tempFormatsList;

		private IntPtr localDllHandle;

		public int MaxFormatSize
		{
			get
			{
				int result;
				MmException.Try(AcmInterop.acmMetrics(this.driverHandle, AcmMetrics.MaxSizeFormat, out result), "acmMetrics");
				return result;
			}
		}

		public string ShortName
		{
			get
			{
				return this.details.shortName;
			}
		}

		public string LongName
		{
			get
			{
				return this.details.longName;
			}
		}

		public IntPtr DriverId
		{
			get
			{
				return this.driverId;
			}
		}

		public IEnumerable<AcmFormatTag> FormatTags
		{
			get
			{
				if (this.formatTags == null)
				{
					if (this.driverHandle == IntPtr.Zero)
					{
						throw new InvalidOperationException("Driver must be opened first");
					}
					this.formatTags = new List<AcmFormatTag>();
					AcmFormatTagDetails acmFormatTagDetails = default(AcmFormatTagDetails);
					acmFormatTagDetails.structureSize = Marshal.SizeOf(acmFormatTagDetails);
					MmException.Try(AcmInterop.acmFormatTagEnum(this.driverHandle, ref acmFormatTagDetails, new AcmInterop.AcmFormatTagEnumCallback(this.AcmFormatTagEnumCallback), IntPtr.Zero, 0), "acmFormatTagEnum");
				}
				return this.formatTags;
			}
		}

		public static bool IsCodecInstalled(string shortName)
		{
			using (IEnumerator<AcmDriver> enumerator = AcmDriver.EnumerateAcmDrivers().GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.ShortName == shortName)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static AcmDriver AddLocalDriver(string driverFile)
		{
			IntPtr intPtr = NativeMethods.LoadLibrary(driverFile);
			if (intPtr == IntPtr.Zero)
			{
				throw new ArgumentException("Failed to load driver file");
			}
			IntPtr procAddress = NativeMethods.GetProcAddress(intPtr, "DriverProc");
			if (procAddress == IntPtr.Zero)
			{
				NativeMethods.FreeLibrary(intPtr);
				throw new ArgumentException("Failed to discover DriverProc");
			}
			IntPtr hAcmDriver;
			MmResult mmResult = AcmInterop.acmDriverAdd(out hAcmDriver, intPtr, procAddress, 0, AcmDriverAddFlags.Function);
			if (mmResult != MmResult.NoError)
			{
				NativeMethods.FreeLibrary(intPtr);
				throw new MmException(mmResult, "acmDriverAdd");
			}
			AcmDriver acmDriver = new AcmDriver(hAcmDriver);
			if (string.IsNullOrEmpty(acmDriver.details.longName))
			{
				acmDriver.details.longName = "Local driver: " + Path.GetFileName(driverFile);
				acmDriver.localDllHandle = intPtr;
			}
			return acmDriver;
		}

		public static void RemoveLocalDriver(AcmDriver localDriver)
		{
			if (localDriver.localDllHandle == IntPtr.Zero)
			{
				throw new ArgumentException("Please pass in the AcmDriver returned by the AddLocalDriver method");
			}
			MmResult arg_3A_0 = AcmInterop.acmDriverRemove(localDriver.driverId, 0);
			NativeMethods.FreeLibrary(localDriver.localDllHandle);
			MmException.Try(arg_3A_0, "acmDriverRemove");
		}

		public static bool ShowFormatChooseDialog(IntPtr ownerWindowHandle, string windowTitle, AcmFormatEnumFlags enumFlags, WaveFormat enumFormat, out WaveFormat selectedFormat, out string selectedFormatDescription, out string selectedFormatTagDescription)
		{
			AcmFormatChoose acmFormatChoose = default(AcmFormatChoose);
			acmFormatChoose.structureSize = Marshal.SizeOf(acmFormatChoose);
			acmFormatChoose.styleFlags = AcmFormatChooseStyleFlags.None;
			acmFormatChoose.ownerWindowHandle = ownerWindowHandle;
			int num = 200;
			acmFormatChoose.selectedWaveFormatPointer = Marshal.AllocHGlobal(num);
			acmFormatChoose.selectedWaveFormatByteSize = num;
			acmFormatChoose.title = windowTitle;
			acmFormatChoose.name = null;
			acmFormatChoose.formatEnumFlags = enumFlags;
			acmFormatChoose.waveFormatEnumPointer = IntPtr.Zero;
			if (enumFormat != null)
			{
				IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(enumFormat));
				Marshal.StructureToPtr(enumFormat, intPtr, false);
				acmFormatChoose.waveFormatEnumPointer = intPtr;
			}
			acmFormatChoose.instanceHandle = IntPtr.Zero;
			acmFormatChoose.templateName = null;
			MmResult mmResult = AcmInterop.acmFormatChoose(ref acmFormatChoose);
			selectedFormat = null;
			selectedFormatDescription = null;
			selectedFormatTagDescription = null;
			if (mmResult == MmResult.NoError)
			{
				selectedFormat = WaveFormat.MarshalFromPtr(acmFormatChoose.selectedWaveFormatPointer);
				selectedFormatDescription = acmFormatChoose.formatDescription;
				selectedFormatTagDescription = acmFormatChoose.formatTagDescription;
			}
			Marshal.FreeHGlobal(acmFormatChoose.waveFormatEnumPointer);
			Marshal.FreeHGlobal(acmFormatChoose.selectedWaveFormatPointer);
			if (mmResult != MmResult.AcmCancelled && mmResult != MmResult.NoError)
			{
				throw new MmException(mmResult, "acmFormatChoose");
			}
			return mmResult == MmResult.NoError;
		}

		public static AcmDriver FindByShortName(string shortName)
		{
			foreach (AcmDriver current in AcmDriver.EnumerateAcmDrivers())
			{
				if (current.ShortName == shortName)
				{
					return current;
				}
			}
			return null;
		}

		public static IEnumerable<AcmDriver> EnumerateAcmDrivers()
		{
			AcmDriver.drivers = new List<AcmDriver>();
			MmException.Try(AcmInterop.acmDriverEnum(new AcmInterop.AcmDriverEnumCallback(AcmDriver.DriverEnumCallback), IntPtr.Zero, (AcmDriverEnumFlags)0), "acmDriverEnum");
			return AcmDriver.drivers;
		}

		private static bool DriverEnumCallback(IntPtr hAcmDriver, IntPtr dwInstance, AcmDriverDetailsSupportFlags flags)
		{
			AcmDriver.drivers.Add(new AcmDriver(hAcmDriver));
			return true;
		}

		private AcmDriver(IntPtr hAcmDriver)
		{
			this.driverId = hAcmDriver;
			this.details = default(AcmDriverDetails);
			this.details.structureSize = Marshal.SizeOf(this.details);
			MmException.Try(AcmInterop.acmDriverDetails(hAcmDriver, ref this.details, 0), "acmDriverDetails");
		}

		public override string ToString()
		{
			return this.LongName;
		}

		public IEnumerable<AcmFormat> GetFormats(AcmFormatTag formatTag)
		{
			if (this.driverHandle == IntPtr.Zero)
			{
				throw new InvalidOperationException("Driver must be opened first");
			}
			this.tempFormatsList = new List<AcmFormat>();
			AcmFormatDetails acmFormatDetails = default(AcmFormatDetails);
			acmFormatDetails.structSize = Marshal.SizeOf(acmFormatDetails);
			acmFormatDetails.waveFormatByteSize = 1024;
			acmFormatDetails.waveFormatPointer = Marshal.AllocHGlobal(acmFormatDetails.waveFormatByteSize);
			acmFormatDetails.formatTag = (int)formatTag.FormatTag;
			MmResult arg_9C_0 = AcmInterop.acmFormatEnum(this.driverHandle, ref acmFormatDetails, new AcmInterop.AcmFormatEnumCallback(this.AcmFormatEnumCallback), IntPtr.Zero, AcmFormatEnumFlags.None);
			Marshal.FreeHGlobal(acmFormatDetails.waveFormatPointer);
			MmException.Try(arg_9C_0, "acmFormatEnum");
			return this.tempFormatsList;
		}

		public void Open()
		{
			if (this.driverHandle == IntPtr.Zero)
			{
				MmException.Try(AcmInterop.acmDriverOpen(out this.driverHandle, this.DriverId, 0), "acmDriverOpen");
			}
		}

		public void Close()
		{
			if (this.driverHandle != IntPtr.Zero)
			{
				MmException.Try(AcmInterop.acmDriverClose(this.driverHandle, 0), "acmDriverClose");
				this.driverHandle = IntPtr.Zero;
			}
		}

		private bool AcmFormatTagEnumCallback(IntPtr hAcmDriverId, ref AcmFormatTagDetails formatTagDetails, IntPtr dwInstance, AcmDriverDetailsSupportFlags flags)
		{
			this.formatTags.Add(new AcmFormatTag(formatTagDetails));
			return true;
		}

		private bool AcmFormatEnumCallback(IntPtr hAcmDriverId, ref AcmFormatDetails formatDetails, IntPtr dwInstance, AcmDriverDetailsSupportFlags flags)
		{
			this.tempFormatsList.Add(new AcmFormat(formatDetails));
			return true;
		}

		public void Dispose()
		{
			if (this.driverHandle != IntPtr.Zero)
			{
				this.Close();
				GC.SuppressFinalize(this);
			}
		}
	}
}
