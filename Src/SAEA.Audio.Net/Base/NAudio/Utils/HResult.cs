using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Utils
{
	public static class HResult
	{
		public const int S_OK = 0;

		public const int S_FALSE = 1;

		public const int E_INVALIDARG = -2147483645;

		private const int FACILITY_AAF = 18;

		private const int FACILITY_ACS = 20;

		private const int FACILITY_BACKGROUNDCOPY = 32;

		private const int FACILITY_CERT = 11;

		private const int FACILITY_COMPLUS = 17;

		private const int FACILITY_CONFIGURATION = 33;

		private const int FACILITY_CONTROL = 10;

		private const int FACILITY_DISPATCH = 2;

		private const int FACILITY_DPLAY = 21;

		private const int FACILITY_HTTP = 25;

		private const int FACILITY_INTERNET = 12;

		private const int FACILITY_ITF = 4;

		private const int FACILITY_MEDIASERVER = 13;

		private const int FACILITY_MSMQ = 14;

		private const int FACILITY_NULL = 0;

		private const int FACILITY_RPC = 1;

		private const int FACILITY_SCARD = 16;

		private const int FACILITY_SECURITY = 9;

		private const int FACILITY_SETUPAPI = 15;

		private const int FACILITY_SSPI = 9;

		private const int FACILITY_STORAGE = 3;

		private const int FACILITY_SXS = 23;

		private const int FACILITY_UMI = 22;

		private const int FACILITY_URT = 19;

		private const int FACILITY_WIN32 = 7;

		private const int FACILITY_WINDOWS = 8;

		private const int FACILITY_WINDOWS_CE = 24;

		public static int MAKE_HRESULT(int sev, int fac, int code)
		{
			return sev << 31 | fac << 16 | code;
		}

		public static int GetHResult(this COMException exception)
		{
			return exception.ErrorCode;
		}
	}
}
