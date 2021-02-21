using Microsoft.Win32;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	internal static class WaveCapabilitiesHelpers
	{
		public static readonly Guid MicrosoftDefaultManufacturerId = new Guid("d5a47fa8-6d98-11d1-a21a-00a0c9223196");

		public static readonly Guid DefaultWaveOutGuid = new Guid("E36DC310-6D9A-11D1-A21A-00A0C9223196");

		public static readonly Guid DefaultWaveInGuid = new Guid("E36DC311-6D9A-11D1-A21A-00A0C9223196");

		public static string GetNameFromGuid(Guid guid)
		{
			string result = null;
			using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\MediaCategories"))
			{
				using (RegistryKey registryKey2 = registryKey.OpenSubKey(guid.ToString("B")))
				{
					if (registryKey2 != null)
					{
						result = (registryKey2.GetValue("Name") as string);
					}
				}
			}
			return result;
		}
	}
}
